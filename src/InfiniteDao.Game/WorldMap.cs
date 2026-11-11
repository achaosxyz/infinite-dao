using Godot;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Game;

public partial class WorldMap : Control
{
    public GameState World { get; set; } = null!;
    public Texture2D Landscape { get; set; } = null!;
    public string Selected { get; set; } = "r06";
    public float Zoom { get; set; } = 1;
    public Vector2 Pan { get; set; }
    public event Action<string>? RegionSelected;
    public event Action<float, Vector2>? ViewChanged;
    private int hovered = -1;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new Vector2(440, 400);
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        ClipContents = true; SetMeta("action", "world-map");
        Resized += QueueRedraw; MouseExited += () => { hovered = -1; QueueRedraw(); };
    }

    private Vector2 Vertex(int column, int row)
    {
        var width = (Size.X - 38) / World.MapColumns; var height = (Size.Y - 38) / World.MapRows;
        var x = column == 0 || column == World.MapColumns ? 0 : MathF.Sin(column * 8.1f + row * 5.2f) * width * .13f;
        var y = row == 0 || row == World.MapRows ? 0 : MathF.Cos(column * 6.4f + row * 7.3f) * height * .13f;
        var point = new Vector2(19 + column * width + x, 19 + row * height + y);
        return (point - Size / 2) * Zoom + Size / 2 + Pan;
    }

    private Vector2[] Polygon(int index)
    {
        var c = index % World.MapColumns; var r = index / World.MapColumns;
        return [Vertex(c, r), Vertex(c + 1, r), Vertex(c + 1, r + 1), Vertex(c, r + 1)];
    }

    public Vector2 RegionCenter(int index)
    {
        var p = Polygon(index); return (p[0] + p[1] + p[2] + p[3]) / 4;
    }

    public override void _Draw()
    {
        if (World is null || Landscape is null || Size.X < 1 || Size.Y < 1) return;
        var sourceSize = Landscape.GetSize();
        var ratio = Math.Max(Size.X / sourceSize.X, Size.Y / sourceSize.Y);
        var crop = Size / ratio;
        DrawTextureRectRegion(Landscape, new Rect2(Size * (1 - Zoom) / 2 + Pan, Size * Zoom), new Rect2((sourceSize - crop) / 2, crop));
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(.02f, .07f, .09f, .44f));
        foreach (var region in World.Regions)
        {
            var color = region.Owner == "" ? NativeTheme.Gold : new Color(World.Sects.Single(s => s.Id == region.Owner).Color);
            var selected = region.Id == Selected;
            var poly = Polygon(region.Index);
            DrawColoredPolygon(poly, new Color(color, selected ? .30f : .13f));
            DrawPolyline([poly[0], poly[1], poly[2], poly[3], poly[0]], new Color(color, selected ? 1 : .5f), selected ? 2.4f : 1, true);
            if (hovered == region.Index) DrawColoredPolygon(poly, new Color(1, 1, 1, .09f));
        }
        foreach (var region in World.Regions)
        {
            var point = RegionCenter(region.Index);
            var owner = World.Sects.SingleOrDefault(s => s.Id == region.Owner);
            var color = owner is null ? NativeTheme.Gold : new Color(owner.Color);
            var selected = region.Id == Selected;
            var capital = owner?.Capital == region.Id;
            var detail = World.Regions.Count <= 36 || Zoom >= 2.2f || selected || capital;
            if (!detail) { DrawCircle(point, 2.2f, new Color(color, .8f)); continue; }
            DrawCircle(point + new Vector2(0, -22), selected ? 7 : 4.5f, color);
            if (capital) DrawArc(point + new Vector2(0, -22), 10, 0, Mathf.Tau, 24, color, 1.3f, true);
            if (selected) DrawArc(point + new Vector2(0, -22), 15, 0, Mathf.Tau, 32, NativeTheme.Paper, 1, true);
            var name = region.Name;
            var labelSize = NativeTheme.Body.GetStringSize(name, fontSize: 16);
            DrawRect(new Rect2(point - new Vector2(labelSize.X / 2 + 6, 9), new Vector2(labelSize.X + 12, 44)), new Color(.025f, .07f, .09f, .80f));
            DrawString(NativeTheme.Body, point + new Vector2(-labelSize.X / 2, 9), name, fontSize: 16, modulate: NativeTheme.Paper);
            var subtitle = owner?.Name ?? "无主之地";
            var subSize = NativeTheme.Body.GetStringSize(subtitle, fontSize: 11);
            DrawString(NativeTheme.Body, point + new Vector2(-subSize.X / 2, 27), subtitle, fontSize: 11, modulate: color);
        }
        foreach (var campaign in World.Campaigns)
        {
            var attacker = World.Sects.Single(s => s.Id == campaign.Attacker);
            var source = RegionCenter(World.Regions.Single(r => r.Id == attacker.Capital).Index);
            var target = RegionCenter(World.Regions.Single(r => r.Id == campaign.Target).Index);
            DrawLine(source, target, new Color(NativeTheme.Cinnabar, .75f), 1.8f, true);
            DrawArc(target, 12, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * (1 - campaign.MonthsRemaining / (float)campaign.Duration), 24, NativeTheme.Gold, 3, true);
        }
        if (HasFocus()) DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), NativeTheme.Gold, false, 2);
    }

    public override void _GuiInput(InputEvent input)
    {
        if (World is null) return;
        if (input is InputEventMouseMotion motion)
        {
            if (motion.ButtonMask.HasFlag(MouseButtonMask.Right)) { Pan += motion.Relative; ConstrainView(); }
            hovered = World.Regions.FindIndex(r => Geometry2D.IsPointInPolygon(motion.Position, Polygon(r.Index)));
            TooltipText = hovered < 0 ? "" : $"{World.Regions[hovered].Name} · 点击查看地域、补给与战役；滚轮放大查看完整名称";
            QueueRedraw();
        }
        if (input is InputEventMouseButton { Pressed: true } wheel && wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            var before = (wheel.Position - Size / 2 - Pan) / Zoom;
            Zoom = Mathf.Clamp(Zoom + (wheel.ButtonIndex == MouseButton.WheelUp ? .3f : -.3f), 1, 5.5f);
            Pan = wheel.Position - Size / 2 - before * Zoom; ConstrainView(); AcceptEvent();
        }
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
        {
            var region = World.Regions.FirstOrDefault(r => Geometry2D.IsPointInPolygon(mouse.Position, Polygon(r.Index)));
            if (region is not null) { GrabFocus(); Select(region.Id); AcceptEvent(); }
        }
        if (input is InputEventKey { Pressed: true } key)
        {
            var index = World.Regions.Single(r => r.Id == Selected).Index;
            var next = key.Keycode switch { Key.Left => index - 1, Key.Right => index + 1, Key.Up => index - World.MapColumns, Key.Down => index + World.MapColumns, _ => index };
            if (next != index) { Select(World.Regions[Math.Clamp(next, 0, World.Regions.Count - 1)].Id); AcceptEvent(); }
        }
    }

    private void Select(string id) { Selected = id; QueueRedraw(); RegionSelected?.Invoke(id); }

    private void ConstrainView()
    {
        var limit = Size * (Zoom - 1) / 2;
        Pan = new Vector2(Mathf.Clamp(Pan.X, -limit.X, limit.X), Mathf.Clamp(Pan.Y, -limit.Y, limit.Y));
        ViewChanged?.Invoke(Zoom, Pan); QueueRedraw();
    }
}
