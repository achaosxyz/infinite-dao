using Godot;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Game;

internal static class NativeTheme
{
    public static readonly Color Ink = new("#eaf0ed");
    public static readonly Color Panel = new("#f7faf8");
    public static readonly Color Raised = new("#e5eeea");
    public static readonly Color Line = new("#afc2ba");
    public static readonly Color Paper = new("#263d42");
    public static readonly Color Muted = new("#63736d");
    public static readonly Color Gold = new("#7b745e");
    public static readonly Color Jade = new("#306e63");
    public static readonly Color Cinnabar = new("#a84f43");
    public static readonly Color DeepJade = new("#173d3c");
    public static readonly Color Brass = new("#aaa58f");
    public static readonly Color Mist = new("#88b4ab");
    public static readonly SystemFont Body = new() { FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC"] };
    public static readonly SystemFont Display = new() { FontNames = ["KaiTi", "STKaiti", "Microsoft YaHei"], FontWeight = 600 };

    public static Theme Create()
    {
        var theme = new Theme { DefaultFont = Body, DefaultFontSize = 15 };
        theme.SetColor("font_color", "Label", Paper);
        theme.SetColor("font_color", "Button", Paper);
        theme.SetColor("font_hover_color", "Button", Gold);
        theme.SetColor("font_disabled_color", "Button", new Color("#92998e"));
        var controlFrame = Frame("hud-control", 8, 10);
        var hoverFrame = Frame("hud-control", 8, 10); hoverFrame.ModulateColor = new Color("#e5f0df");
        var pressedFrame = Frame("hud-control", 8, 10); pressedFrame.ModulateColor = new Color("#c6ddcf");
        var disabledFrame = Frame("hud-control", 8, 10); disabledFrame.ModulateColor = new Color(.94f, .94f, .91f, .65f);
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            theme.SetStylebox("normal", type, controlFrame); theme.SetStylebox("hover", type, hoverFrame);
            theme.SetStylebox("pressed", type, pressedFrame); theme.SetStylebox("disabled", type, disabledFrame);
        }
        theme.SetStylebox("focus", "Button", Box(Colors.Transparent, Jade, 2));
        theme.SetStylebox("normal", "LineEdit", Box(Ink, Line));
        theme.SetStylebox("focus", "LineEdit", Box(Ink, Gold));
        theme.SetColor("font_color", "LineEdit", Paper);
        theme.SetColor("font_placeholder_color", "LineEdit", Muted);
        theme.SetColor("font_color", "OptionButton", Paper);
        theme.SetStylebox("focus", "OptionButton", Box(Colors.Transparent, Gold, 2));
        theme.SetStylebox("panel", "PopupMenu", Box(Panel, Gold));
        theme.SetColor("font_color", "PopupMenu", Paper);
        theme.SetColor("font_hover_color", "PopupMenu", DeepJade);
        theme.SetStylebox("hover", "PopupMenu", Box(Raised, Mist));
        theme.SetColor("font_color", "CheckButton", Paper);
        theme.SetStylebox("panel", "PanelContainer", Box(Panel, Line));
        theme.SetStylebox("background", "ProgressBar", Box(Ink, Line, 1, 0));
        theme.SetStylebox("fill", "ProgressBar", Box(Mist, Jade, 0, 0));
        theme.SetConstant("separation", "VBoxContainer", 12);
        theme.SetConstant("separation", "HBoxContainer", 12);
        theme.SetConstant("h_separation", "GridContainer", 12);
        theme.SetConstant("v_separation", "GridContainer", 12);
        return theme;
    }

    public static StyleBoxFlat Box(Color background, Color border, int width = 1, int padding = 12) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = width, BorderWidthTop = width, BorderWidthRight = width, BorderWidthBottom = width,
        ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
    };

    public static VBoxContainer Stack(int gap = 12)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", gap); return box;
    }

    public static HBoxContainer Row(int gap = 12)
    {
        var box = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", gap); return box;
    }

    public static MarginContainer Pad(Control child, int padding = 20)
    {
        var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        foreach (var side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, padding);
        margin.AddChild(child); return margin;
    }

    public static PanelContainer Card(Control child, Color? accent = null, int padding = 18)
    {
        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", accent is null || accent == Line || accent == Brass || accent == Gold ? Frame("hud-panel", padding) : Box(Panel, accent.Value, 1, padding));
        card.AddChild(child); return card;
    }

    public static StyleBoxTexture Frame(string asset, int padding = 12, int corner = 24) => new()
    {
        Texture = GD.Load<Texture2D>("res://Assets/" + asset + ".svg"),
        TextureMarginLeft = corner, TextureMarginRight = corner, TextureMarginTop = corner, TextureMarginBottom = corner,
        ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding
    };

    private static readonly SystemFont Numeric = new() { FontNames = ["Bahnschrift", "Segoe UI"] };

    public static Label Value(string text, int size = 24, Color? color = null)
    {
        var label = Text(text, size, color ?? DeepJade); label.AddThemeFontOverride("font", Numeric); return label;
    }

    public static Label Text(string text, int size = 15, Color? color = null, bool wrap = false)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color is not null) label.AddThemeColorOverride("font_color", color.Value);
        if (wrap) { label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; }
        return label;
    }

    public static Label Heading(string text, int size = 28, Color? color = null)
    {
        var label = Text(text, size, color); label.AddThemeFontOverride("font", Display); return label;
    }

    public static Button Button(string text, string action, Action handler, bool disabled = false, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 42), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, Disabled = disabled, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.SetMeta("action", action);
        if (primary)
        {
            button.AddThemeStyleboxOverride("normal", Frame("hud-action", 8, 16));
            var hover = Frame("hud-action", 8, 16); hover.ModulateColor = new Color(1.12f, 1.12f, 1.05f);
            var pressed = Frame("hud-action", 8, 16); pressed.ModulateColor = new Color(.85f, .9f, .86f);
            button.AddThemeStyleboxOverride("hover", hover); button.AddThemeStyleboxOverride("pressed", pressed);
            button.AddThemeColorOverride("font_color", Panel);
            button.AddThemeColorOverride("font_hover_color", Panel);
            button.AddThemeColorOverride("font_pressed_color", Panel);
        }
        button.Pressed += handler; return button;
    }

    public static TextureRect Portrait(IReadOnlyList<Texture2D> library, int index, int width = 120, int height = 120) => new()
    {
        Texture = height > width * 1.4f ? library[index] : new AtlasTexture { Atlas = library[index], Region = new Rect2(136, 8, 240, 240) },
        CustomMinimumSize = new Vector2(width, height), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = height > width * 1.4f ? TextureRect.StretchModeEnum.KeepAspectCentered : TextureRect.StretchModeEnum.KeepAspectCovered,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    public static Control Emblem(Sect sect, int size = 48, bool inverse = false)
    {
        var color = inverse ? Panel : new Color(sect.Color).Darkened(.4f);
        var emblem = new PanelContainer { CustomMinimumSize = new Vector2(size, size), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        emblem.SetMeta("sect_emblem", true);
        var style = Box(inverse ? DeepJade : Raised, Brass, 1, 6);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = size / 2;
        emblem.AddThemeStyleboxOverride("panel", style);
        emblem.AddChild(Icon(sect.Sigil, size - 12, color)); emblem.TooltipText = sect.Name; return emblem;
    }

    public static Control Icon(string kind, int size = 24, Color? color = null) => new HudGlyph
    {
        Kind = kind, Tint = color ?? Jade, CustomMinimumSize = new Vector2(size, size),
        MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
    };

    public static Control Calendar(int month, int size = 58) => new CalendarGlyph
    {
        Month = month, CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore,
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
    };

    public static ScrollContainer Scroll(Control content)
    {
        var scroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(content); return scroll;
    }

    public static Control Space(int height = 0, bool expand = false) => new()
    {
        CustomMinimumSize = new Vector2(0, height), SizeFlagsVertical = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };
    public static string Date(int month) => $"青岚历 {month / 12 + 1} 年 · {month % 12 + 1} 月";
    public static string Number(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    public static string Policy(string key) => WorldFactory.PolicyNames[Array.IndexOf(WorldFactory.Policies, key)];
}

internal partial class HudGlyph : Control
{
    public string Kind { get; set; } = "map";
    public Color Tint { get; set; } = NativeTheme.Jade;

    public override void _Draw()
    {
        var scale = Math.Min(Size.X, Size.Y) / 32;
        DrawSetTransform((Size - Vector2.One * 32 * scale) / 2, 0, Vector2.One * scale);
        void Stroke(params Vector2[] points) => DrawPolyline(points, Tint, 1.7f, true);
        switch (Kind)
        {
            case "剑":
                Stroke(new(16, 3), new(20, 9), new(17, 22), new(13, 22), new(12, 9), new(16, 3));
                Stroke(new(8, 23), new(24, 23)); Stroke(new(16, 23), new(16, 29));
                Stroke(new(3, 18), new(6, 15), new(10, 17)); Stroke(new(22, 15), new(26, 13), new(29, 16)); break;
            case "焰":
                Vector2[] flame = [new(16, 3), new(20, 13), new(25, 9), new(28, 20), new(24, 27), new(15, 29), new(6, 25), new(4, 18), new(10, 9), new(10, 20), new(16, 3)];
                DrawColoredPolygon(flame, new Color(Tint, .14f)); Stroke(flame); Stroke(new(13, 25), new(16, 15), new(20, 23)); break;
            case "药":
                Stroke(new(16, 29), new(16, 10)); Stroke(new(16, 19), new(7, 14), new(4, 6), new(12, 9), new(16, 16));
                Stroke(new(16, 23), new(24, 18), new(28, 9), new(20, 12), new(16, 20)); DrawCircle(new(16, 6), 2, NativeTheme.Brass); break;
            case "器":
                Stroke(new(3, 12), new(13, 12), new(13, 8), new(21, 8), new(21, 12), new(29, 12), new(24, 18), new(18, 18), new(18, 24), new(24, 27), new(8, 27), new(14, 24), new(14, 18), new(7, 18), new(3, 12));
                DrawLine(new(16, 2), new(16, 5), NativeTheme.Brass, 1.4f, true); break;
            case "澜":
                DrawArc(new(16, 13), 9, 0, Mathf.Tau, 40, Tint, 1.2f, true); DrawArc(new(16, 13), 6, -.7f, 1.7f, 20, NativeTheme.Brass, 1, true);
                Stroke(new(3, 26), new(9, 24), new(16, 27), new(23, 24), new(29, 26)); Stroke(new(7, 30), new(16, 28), new(25, 30)); break;
            case "霜":
                for (var i = 0; i < 6; i++)
                {
                    var direction = Vector2.FromAngle(i * Mathf.Tau / 6 - Mathf.Pi / 2); var end = new Vector2(16, 16) + direction * 13;
                    DrawLine(new(16, 16), end, Tint, 1.3f, true);
                    DrawLine(end, end - direction.Rotated(.6f) * 5, Tint, 1.1f, true); DrawLine(end, end - direction.Rotated(-.6f) * 5, Tint, 1.1f, true);
                }
                DrawCircle(new(16, 16), 2.2f, NativeTheme.Brass); break;
            case "雷":
                Vector2[] bolt = [new(19, 2), new(7, 18), new(15, 18), new(12, 30), new(26, 12), new(18, 12), new(19, 2)];
                DrawColoredPolygon(bolt, new Color(Tint, .18f)); Stroke(bolt); DrawArc(new(16, 16), 14, -.5f, 1, 16, NativeTheme.Brass, 1, true); break;
            case "禅":
                Stroke(new(16, 4), new(9, 16), new(16, 25), new(23, 16), new(16, 4));
                Stroke(new(16, 25), new(5, 23), new(2, 12), new(10, 16)); Stroke(new(16, 25), new(27, 23), new(30, 12), new(22, 16));
                DrawLine(new(5, 28), new(27, 28), NativeTheme.Brass, 1.2f, true); break;
            case "星":
                var star = Enumerable.Range(0, 11).Select(i => new Vector2(16, 16) + Vector2.FromAngle(i * Mathf.Tau / 10 - Mathf.Pi / 2) * (i % 2 == 0 ? 13 : 5)).ToArray();
                DrawColoredPolygon(star[..10], new Color(Tint, .13f)); Stroke(star); DrawCircle(new(16, 16), 2.3f, NativeTheme.Brass); break;
            case "影":
                Stroke(new(11, 29), new(17, 3)); Stroke(new(11, 23), new(16, 24)); Stroke(new(13, 14), new(18, 15));
                Stroke(new(14, 18), new(5, 12), new(4, 5), new(13, 12)); Stroke(new(17, 15), new(25, 7), new(29, 8), new(20, 17));
                DrawLine(new(22, 22), new(27, 26), NativeTheme.Brass, 1.2f, true); break;
            case "stone":
                Vector2[] crystal = [new(16, 2), new(27, 12), new(24, 25), new(16, 30), new(6, 24), new(4, 12), new(16, 2)];
                DrawColoredPolygon(crystal, new Color(Tint, .15f)); Stroke(crystal);
                Stroke(new(16, 2), new(12, 12), new(16, 30), new(20, 12), new(16, 2)); Stroke(new(4, 12), new(27, 12)); break;
            case "herb":
                Stroke(new(15, 29), new(17, 9), new(21, 3));
                Stroke(new(16, 21), new(5, 19), new(3, 9), new(11, 11), new(16, 21));
                Stroke(new(17, 14), new(20, 7), new(29, 6), new(25, 16), new(17, 19)); break;
            case "people":
                DrawCircle(new(16, 8), 4, Tint); DrawCircle(new(6, 11), 3, Tint); DrawCircle(new(26, 11), 3, Tint);
                DrawArc(new(16, 25), 8, Mathf.Pi, Mathf.Tau, 18, Tint, 5, true);
                DrawArc(new(5, 25), 4, Mathf.Pi, Mathf.Tau, 12, Tint, 4, true); DrawArc(new(27, 25), 4, Mathf.Pi, Mathf.Tau, 12, Tint, 4, true); break;
            case "power":
                Stroke(new(4, 3), new(24, 24), new(28, 29)); Stroke(new(28, 3), new(8, 24), new(4, 29));
                Stroke(new(18, 25), new(26, 17)); Stroke(new(6, 17), new(14, 25)); break;
            case "stability":
                Stroke(new(3, 5), new(16, 2), new(29, 5), new(26, 22), new(16, 30), new(6, 22), new(3, 5));
                Stroke(new(16, 7), new(16, 24)); Stroke(new(9, 12), new(16, 18), new(23, 12)); break;
            case "sect":
                Stroke(new(3, 13), new(16, 5), new(29, 13)); Stroke(new(3, 15), new(29, 15));
                Stroke(new(8, 16), new(8, 26)); Stroke(new(16, 16), new(16, 26)); Stroke(new(24, 16), new(24, 26)); Stroke(new(2, 28), new(30, 28)); break;
            case "treasury":
                Stroke(new(4, 11), new(28, 11), new(29, 28), new(3, 28), new(4, 11));
                Stroke(new(4, 11), new(8, 5), new(24, 5), new(28, 11)); Stroke(new(3, 18), new(29, 18)); DrawRect(new Rect2(13, 15, 6, 7), Tint, false, 1.5f); break;
            case "history":
                Stroke(new(16, 7), new(4, 3), new(3, 26), new(16, 30), new(29, 26), new(28, 3), new(16, 7), new(16, 30));
                Stroke(new(7, 11), new(12, 13)); Stroke(new(20, 13), new(25, 11)); Stroke(new(7, 17), new(12, 19)); break;
            case "diplomacy":
                Stroke(new(2, 15), new(9, 8), new(16, 11), new(23, 8), new(30, 15), new(21, 25), new(14, 26), new(2, 15));
                Stroke(new(10, 10), new(6, 16), new(13, 22)); Stroke(new(23, 10), new(16, 17), new(12, 14)); break;
            case "lands":
                Stroke(new(7, 29), new(7, 3), new(27, 4), new(23, 11), new(27, 18), new(7, 17)); break;
            default:
                Stroke(new(2, 27), new(10, 12), new(16, 20), new(23, 6), new(31, 27), new(2, 27));
                Stroke(new(19, 14), new(23, 17), new(26, 14)); break;
        }
    }
}

internal partial class CalendarGlyph : Control
{
    public int Month { get; set; }

    public override void _Draw()
    {
        var center = Size / 2; var radius = Math.Min(Size.X, Size.Y) / 2 - 2;
        DrawCircle(center, radius, NativeTheme.Panel);
        DrawArc(center, radius, 0, Mathf.Tau, 64, NativeTheme.Brass, 1.3f, true);
        DrawArc(center, radius - 4, 0, Mathf.Tau, 64, new Color(NativeTheme.Brass, .6f), .7f, true);
        for (var i = 0; i < 12; i++)
        {
            var direction = Vector2.FromAngle(i * Mathf.Tau / 12 - Mathf.Pi / 2);
            DrawLine(center + direction * (radius - 6), center + direction * (radius - (i % 3 == 0 ? 12 : 9)), NativeTheme.Gold, 1, true);
        }
        var angle = Month % 12 * Mathf.Tau / 12 - Mathf.Pi / 2;
        DrawLine(center, center + Vector2.FromAngle(angle) * (radius - 7), NativeTheme.Jade, 1.8f, true);
        DrawColoredPolygon([center + new Vector2(0, -5), center + new Vector2(4, 0), center + new Vector2(0, 5), center + new Vector2(-4, 0)], NativeTheme.Brass);
    }
}
