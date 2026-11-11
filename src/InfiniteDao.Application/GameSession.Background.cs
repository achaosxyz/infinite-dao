using System.Collections.Concurrent;
using InfiniteDao.Agents.Configuration;
using InfiniteDao.Agents.Providers;
using InfiniteDao.Agents.Runtime;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Application;

public sealed partial class GameSession
{
    private const int AutomaticRetries = 3;
    private readonly Dictionary<string, BackgroundJob> backgroundJobs = [];
    private readonly ConcurrentQueue<BackgroundUpdate> backgroundUpdates = new();
    private GameState? latestObservation;
    private string activity = "";
    private int backgroundEpoch, activeBackgroundJobs;
    public bool HasBackgroundWork => Volatile.Read(ref activeBackgroundJobs) > 0;

    // Like Advance/Execute, callers serialize this operation through Gate. Never waits for a model.
    public async Task<bool> PumpAgents(CancellationToken cancellationToken)
    {
        if (State is null) return false;
        if (backgroundUpdates.IsEmpty) { ScheduleBackground(State, cancellationToken); return false; }
        var next = State.Copy();
        var changed = DrainBackground(next);
        if (changed) await SaveState(next, advanceRevision: false);
        ScheduleBackground(State!, cancellationToken);
        return changed;
    }

    public void StopAgents()
    {
        backgroundEpoch++;
        foreach (var job in backgroundJobs.Values) job.Cancel();
        backgroundJobs.Clear();
        Volatile.Write(ref activeBackgroundJobs, 0);
        Volatile.Write(ref latestObservation, null);
        Volatile.Write(ref activity, "");
    }

    private void ScheduleBackground(GameState world, CancellationToken cancellationToken)
    {
        Volatile.Write(ref latestObservation, world);
        foreach (var job in backgroundJobs.Values)
            if (world.Mode != "llm" || world.Status != "playing" || !Relevant(world, job)) job.Cancel();
        if (world.Mode != "llm" || world.Status != "playing") return;
        LlmConfiguration config;
        try { config = ReadConfiguration(); }
        catch (GameRuleException)
        {
            backgroundUpdates.Enqueue(new(backgroundEpoch, Failed: true, Status: "模型配置不可用，已切换离线规则；请检查本地配置后手动恢复智能世界。"));
            return;
        }
        var limit = config.Runtime!.MaxConcurrentRequests;
        foreach (var sect in world.Sects.Where(s => s.Id != world.PlayerId && !s.Eliminated).OrderBy(s => s.Id))
        {
            var memory = world.AgentMemories.SingleOrDefault(m => m.SectId == sect.Id) ?? new SectAgentMemory { SectId = sect.Id };
            var trigger = SectAgentSchedule.WakeReason(world, sect, memory);
            if (trigger != "") Start(sect.Id, sect.Name, sect.Id, trigger);
        }
        Volatile.Write(ref activeBackgroundJobs, backgroundJobs.Count);
        var retrying = backgroundJobs.Values.FirstOrDefault(j => Volatile.Read(ref j.RetryNumber) > 0);
        if (retrying is not null)
            Volatile.Write(ref activity, $"{retrying.Name} Agent自动重试 {Volatile.Read(ref retrying.RetryNumber)}/3 · 世界继续演进");
        else if (backgroundJobs.Count > 0)
            Volatile.Write(ref activity, $"{backgroundJobs.Count} 个Agent后台议政中 · 世界继续演进");
        else if (backgroundJobs.Count == 0) Volatile.Write(ref activity, "");

        void Start(string key, string name, string sectId, string trigger)
        {
            if (backgroundJobs.Count >= limit || backgroundJobs.ContainsKey(key)) return;
            var job = new BackgroundJob(key, name, sectId, trigger, cancellationToken);
            backgroundJobs.Add(key, job);
            _ = RunBackground(job, backgroundEpoch, config);
        }
    }

    private async Task RunBackground(BackgroundJob job, int epoch, LlmConfiguration config)
    {
        var token = job.Cancellation.Token;
        try
        {
            // Task.Run isolates snapshot serialization and tool preparation from the simulation too.
            await Task.Run(async () =>
            {
                for (var attempt = 0; attempt <= AutomaticRetries; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var snapshot = Volatile.Read(ref latestObservation)!.Copy();
                        if (!Relevant(snapshot, job)) break;
                        var sect = snapshot.Sects.Single(s => s.Id == job.SectId);
                        var memory = snapshot.AgentMemories.SingleOrDefault(m => m.SectId == sect.Id) ?? new SectAgentMemory { SectId = sect.Id };
                        memory.LastTrigger = job.Trigger;
                        await new SectAgentLoop(client).RunAsync(snapshot, sect, memory, config, Request,
                            (tokens, _) => { backgroundUpdates.Enqueue(new(epoch, Job: job, Tokens: tokens)); return Task.CompletedTask; }, token,
                            _ => { backgroundUpdates.Enqueue(new(epoch, Job: job, ContextChecks: 1)); return Task.CompletedTask; });
                        backgroundUpdates.Enqueue(new(epoch, Job: job, Memory: memory));
                        Volatile.Write(ref job.RetryNumber, 0);
                        break;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        // Never persist provider bodies, authentication details or hidden reasoning.
                        if (attempt == AutomaticRetries)
                        {
                            backgroundUpdates.Enqueue(new(epoch, Job: job, Failed: true,
                                Status: $"{job.Name} Agent自动重试3次仍失败，已切换离线规则，时间继续流逝。可在设置中手动恢复智能世界。"));
                            break;
                        }
                        Volatile.Write(ref job.RetryNumber, attempt + 1);
                        backgroundUpdates.Enqueue(new(epoch, Job: job, Status: $"{job.Name} Agent调用失败，自动重试 {attempt + 1}/3 · 世界继续演进"));
                        await Task.Delay(TimeSpan.FromSeconds(1 << attempt), token);
                    }
                }
            }, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            backgroundUpdates.Enqueue(new(epoch, Job: job, Finished: true));
            job.Cancellation.Dispose();
        }

        Task Request(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Interlocked.Increment(ref callsUsed);
            backgroundUpdates.Enqueue(new(epoch, Job: job, Requests: 1));
            return Task.CompletedTask;
        }
    }

    private static bool Relevant(GameState world, BackgroundJob job) =>
        world.Sects.Any(s => s.Id == job.SectId && !s.Eliminated && s.Id != world.PlayerId);

    private sealed class BackgroundJob(string key, string name, string sectId, string trigger, CancellationToken cancellationToken)
    {
        public string Key { get; } = key;
        public string Name { get; } = name;
        public string SectId { get; } = sectId;
        public string Trigger { get; } = trigger;
        public int RetryNumber;
        public CancellationTokenSource Cancellation { get; } = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        public void Cancel() { try { Cancellation.Cancel(); } catch (ObjectDisposedException) { /* Finished; result still awaits collection. */ } }
    }

    private sealed record BackgroundUpdate(int Epoch, BackgroundJob? Job = null, int Requests = 0, long Tokens = 0, int ContextChecks = 0,
        string? Status = null, SectAgentMemory? Memory = null, bool Finished = false, bool Failed = false);
}
