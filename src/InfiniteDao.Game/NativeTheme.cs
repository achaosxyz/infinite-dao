using Godot;
using InfiniteDao.Core.Game;

namespace InfiniteDao.Game;

internal static class NativeTheme
{
    public static readonly Color Ink = new("#0b1820");
    public static readonly Color Panel = new("#132832");
    public static readonly Color Raised = new("#1b3540");
    public static readonly Color Line = new("#30505a");
    public static readonly Color Paper = new("#e5eddf");
    public static readonly Color Muted = new("#9ab0ae");
    public static readonly Color Gold = new("#d5b778");
    public static readonly Color Jade = new("#79c6b0");
    public static readonly Color Cinnabar = new("#e99a7d");
    public static readonly SystemFont Body = new() { FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC"] };
    public static readonly SystemFont Display = new() { FontNames = ["KaiTi", "STKaiti", "Microsoft YaHei"], FontWeight = 600 };

    public static Theme Create()
    {
        var theme = new Theme { DefaultFont = Body, DefaultFontSize = 15 };
        theme.SetColor("font_color", "Label", Paper);
        theme.SetColor("font_color", "Button", Paper);
        theme.SetColor("font_hover_color", "Button", Gold);
        theme.SetColor("font_disabled_color", "Button", new Color("#647a7c"));
        theme.SetStylebox("normal", "Button", Box(Panel, Line));
        theme.SetStylebox("hover", "Button", Box(Raised, Gold));
        theme.SetStylebox("pressed", "Button", Box(new Color("#25493f"), Jade));
        theme.SetStylebox("disabled", "Button", Box(new Color("#112229"), new Color("#233b44")));
        theme.SetStylebox("focus", "Button", Box(Colors.Transparent, Gold, 2));
        theme.SetStylebox("normal", "LineEdit", Box(Ink, Line));
        theme.SetStylebox("focus", "LineEdit", Box(Ink, Gold));
        theme.SetColor("font_color", "LineEdit", Paper);
        theme.SetColor("font_color", "OptionButton", Paper);
        theme.SetStylebox("normal", "OptionButton", Box(Panel, Line));
        theme.SetStylebox("hover", "OptionButton", Box(Raised, Gold));
        theme.SetStylebox("focus", "OptionButton", Box(Colors.Transparent, Gold, 2));
        theme.SetStylebox("panel", "PopupMenu", Box(Panel, Gold));
        theme.SetColor("font_color", "PopupMenu", Paper);
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
        CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
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
        card.AddThemeStyleboxOverride("panel", Box(Panel, accent ?? Line, 1, padding)); card.AddChild(child); return card;
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
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 42), Disabled = disabled, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.SetMeta("action", action);
        if (primary)
        {
            button.AddThemeStyleboxOverride("normal", Box(new Color("#285446"), Jade));
            button.AddThemeColorOverride("font_color", Paper);
        }
        button.Pressed += handler; return button;
    }

    public static TextureRect Portrait(Texture2D atlas, int index, int width = 120, int height = 120) => new()
    {
        Texture = new AtlasTexture { Atlas = atlas, Region = new Rect2(index % 3 * 512, index / 3 * 512, 512, 512) },
        CustomMinimumSize = new Vector2(width, height), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore
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
