using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Walgelijk;
using Walgelijk.AssetManager;
using Walgelijk.Onion;
using Walgelijk.Onion.Controls;
using Walgelijk.Onion.Decorators;
using Walgelijk.SimpleDrawing;

namespace MIR;

public class AnimationTestingSystem : Walgelijk.System
{
    public override void Initialise()
    {
        if (Scene.FindAnyComponent<AnimationTestingComponent>(out var data) && data != null)
            data.Animations = Assets.EnumerateFolder("data/animations", SearchOption.AllDirectories).Select(Assets.Load<CharacterAnimation>).ToArray();
    }

    public override void Update()
    {
        if (!Scene.FindAnyComponent<AnimationTestingComponent>(out var data))
            return;

        if (!Scene.FindAnyComponent<CharacterComponent>(out var character))
            return;

        if (!Scene.TryGetComponentFrom<PlayerComponent>(character.Entity, out var playerComponent))
            return;

        if (Scene.FindAnyComponent<CameraComponent>(out var cam))
            cam.ClearColour = new Color(26, 26, 26);

        if (Input.IsKeyPressed(Key.Space))
        {
            if (character.MainAnimation == null && data.LastAnimation != null)
                character.PlayAnimation(data.LastAnimation);
            else
                character.EnableAnimationClock = !character.EnableAnimationClock;
        }

        if (character.MainAnimation != null)
        {
            var mainAnim = character.MainAnimation;
            float keyframeTime = mainAnim.Animation.TotalDuration / mainAnim.MaxKeyCount;

            if (Input.IsKeyPressed(Key.Comma))
            {
                mainAnim.UnscaledTimer -= 0.001f;
                mainAnim.UnscaledTimer = float.Floor(mainAnim.UnscaledTimer / keyframeTime) * keyframeTime;
                mainAnim.UnscaledTimer = float.Clamp(mainAnim.UnscaledTimer, 0, mainAnim.Animation.TotalDuration);
            }

            if (Input.IsKeyPressed(Key.Period))
            {
                mainAnim.UnscaledTimer += 0.001f;
                mainAnim.UnscaledTimer = float.Ceiling(mainAnim.UnscaledTimer / keyframeTime) * keyframeTime;
                mainAnim.UnscaledTimer = float.Clamp(mainAnim.UnscaledTimer, 0, mainAnim.Animation.TotalDuration);
            }
        }

        Ui.Theme.Font(Fonts.CascadiaMono);

        playerComponent.RespondToUserInput = false;

        const float progressBarHeight = 90;
        var nofilter = string.IsNullOrWhiteSpace(data.Filter);

        Ui.Layout.Move(5, 5).Size(263, 32);
        Ui.StringInputBox(ref data.Filter, new(placeholder: "Filter...", maxLength: short.MaxValue));

        Ui.Layout.Move(302 - 32, 5).Size(32, 32);
        if (Ui.ClickImageButton(Textures.UserInterface.MenuBack.Value, ImageContainmentMode.Stretch) && !nofilter)
            data.Filter = string.Empty;

        Ui.Layout.Move(0, 48).Size(300, Window.Size.Y - progressBarHeight - 10 - 48);
        //layout.VerticalLayout();
        Ui.StartScrollView();
        float o = 0;
        for (int i = 0; i < data.Animations.Length; i++)
        {
            var animation = data.Animations[i].Value;
            if ((nofilter || animation.Name.Contains(data.Filter, StringComparison.InvariantCultureIgnoreCase)))
            {
                Ui.Layout.Move(0, o).Size(200, 32).FitContainer(1, null).CenterHorizontal();
                o += (32 + Onion.Theme.Base.Padding);
                //if (Gui.ClickButton(name, default, new Vector2(200, 32), HorizontalTextAlign.Left, optionalId: i))
                if (Ui.ClickButton(animation.Name, identity: i))
                {
                    if (!data.BlendAnimationFlag)
                    {
                        character.Animations.Clear();
                        character.ResetAnimation();
                    }
                    character.PlayAnimation(animation);
                    data.LastAnimation = animation;
                }
            }
        }
        Ui.End();

        Ui.Layout.Width(300).Height(400).StickRight().StickTop().VerticalLayout();
        Ui.StartScrollView();
        {
            if (character.MainAnimation != null)
            {
                Ui.Label("Name: " + character.MainAnimation!.Animation.Name);
                Ui.Label("Group: " + character.MainAnimation!.Animation.Group);
                //Ui.Label("Smooth: " + character.MainAnimation!.Animation.DoSmoothing);
                Ui.Label("Relative: " + character.MainAnimation!.Animation.RelativeHandPosition);
                Ui.Spacer(16);
            }
            Ui.Theme.FontSize(18).Once();
            Ui.Label("Active constraints:");
            var v = character.GetAnimationConstraints();
            Ui.Layout.PreferredSize().FitWidth().StickLeft();
            Ui.Theme.Text(Colors.Red).Once();
            Ui.TextRect(v.ToString(), HorizontalTextAlign.Left, VerticalTextAlign.Top);
        }
        Ui.End();

        if (data.LoopAnimationFlag && data.LastAnimation != null && (character.MainAnimation == null || character.MainAnimation.IsAlmostOver()))
            character.PlayAnimation(data.LastAnimation);

        Ui.Layout.Move(0, Window.Size.Y - progressBarHeight).Size(Window.Size.X, progressBarHeight / 2).HorizontalLayout();
        Ui.StartScrollView();
        {
            Ui.Layout.Width(256).FitHeight();
            if (Ui.Button("main menu"))
                Game.Scene = MainMenuScene.Load(Game);

            if (character.EquippedWeapon.IsValid(Scene))
            {
                Ui.Layout.Width(256).FitHeight();
                if (Ui.Button("drop weapon"))
                    character.DropWeapon(Scene);
            }

            Ui.Layout.Width(150).FitHeight();
            if (Ui.Button("flip character"))
            {
                character.Positioning.IsFlipped = !character.Positioning.IsFlipped;
                character.NeedsLookUpdate = true;
            }

            Ui.Layout.Width(128).FitHeight();
            Ui.Checkbox(ref data.BlendAnimationFlag, "blend");

            Ui.Layout.Width(128).FitHeight();
            Ui.Checkbox(ref data.LoopAnimationFlag, "loop");

            Ui.Layout.Width(150).FitHeight();
            Ui.Checkbox(ref character.EnableAnimationClock, "playback");

            Ui.Layout.Width(200).FitHeight();
            Ui.Checkbox(ref data.ShowCurveDebugger, "show curves");

        }
        Ui.End();

        {
            float time = character.MainAnimation != null ? character.MainAnimation.UnscaledTimer : 0;
            float max = character.MainAnimation != null ? character.MainAnimation.Animation.TotalDuration : 0;

            string keyframeText = character.MainAnimation != null ? $"{(int)(time / max * (character.MainAnimation.MaxKeyCount + 1))} | {time:0.##}" : string.Empty;

            Ui.Layout.Move(0, Window.Size.Y - progressBarHeight / 2).Size(Window.Size.X, progressBarHeight / 2);
            Ui.Theme.OutlineWidth(2).OutlineColour(Colors.White).Once();
            Ui.Decorate(new TimelineConstraintDecorator(character.MainAnimation?.Animation ?? data.LastAnimation));
            if (Ui.FloatSlider(ref time, Direction.Horizontal, new MinMax<float>(0, max), label: keyframeText))
            {
                if (!character.IsPlayingAnimation && data.LastAnimation != null)
                    character.PlayAnimation(data.LastAnimation);
                if (character.MainAnimation != null)
                    character.MainAnimation.UnscaledTimer = time;
            }
            Ui.Layout.Move(0, Window.Size.Y - progressBarHeight / 2).Size(Window.Size.X, progressBarHeight / 2);
        }

        Draw.Reset();
        Draw.Colour = Colors.Red;
        Draw.Line(new Vector2(-1000, Level.CurrentLevel?.GetFloorLevelAt(0) ?? 0), new Vector2(1000, Level.CurrentLevel?.GetFloorLevelAt(0) ?? 0), 5);

        Ui.Theme.Pop();

        if (data.ShowCurveDebugger)
        {
            var rr = new Rect(0, 0, Window.Width, 300).Translate(0, 10);
            rr.MinX += 310;
            rr.MaxX -= 310;

            Draw.Reset();
            Draw.ScreenSpace = true;
            Draw.Order = RenderOrders.UserInterface;
            Draw.Colour = Colors.Black.WithAlpha(0.9f);
            Draw.Quad(rr);
            rr = rr.Expand(-10);

            Draw.Colour = Colors.GreenYellow;
            var cursor = rr.GetCenter() with { X = rr.MinX };
            float valScale = 0.01f;

            if (character.MainAnimation != null)
            {
                var active = character.MainAnimation;
                var anim = active.Animation;

                var limb = anim.BodyAnimation;
                if (limb != null)
                {
                    float durationRatio = anim.TotalDuration / limb.Duration;
                    var curve = limb.RotationCurve;
                    if (curve != null)
                    {
                        float min = curve.Keys.Min(static k => k.Value);
                        float max = curve.Keys.Max(static k => k.Value);

                        foreach (var item in curve.Keys)
                        {
                            var a = cursor;
                            var b = new Vector2(
                                float.Lerp(rr.MinX, rr.MaxX, item.Position / durationRatio),
                                float.Lerp(rr.MaxY, rr.MinY, Utilities.MapRange(min, max, 0, 1, item.Value))
                            );

                            if (item.Position > float.Epsilon)
                                Draw.Line(a, b, 2);
                            Draw.Circle(b, new(4));

                            cursor = b;
                        }

                        var linePos = float.Lerp(rr.MinX, rr.MaxX, active.UnscaledTimer / anim.TotalDuration);
                        var t = active.UnscaledTimer / anim.TotalDuration * durationRatio;
                        var valAtTime = curve.Evaluate(t);
                        var screenPos = new Vector2(linePos,
                            float.Lerp(rr.MaxY, rr.MinY, Utilities.MapRange(min, max, 0, 1, valAtTime)));

                        Draw.Colour = Colors.Cyan;
                        Draw.Font = Fonts.CascadiaMono;
                        Draw.Line(new Vector2(linePos, rr.MinY), new Vector2(linePos, rr.MaxY), 1);
                        var vts = $"{valAtTime:0.###}";
                        Draw.Colour = Colors.Black;
                        screenPos.Y -= 10;
                        screenPos.X += 10;
                        Draw.Text(vts, screenPos + new Vector2(0, 1), Vector2.One, HorizontalTextAlign.Left, VerticalTextAlign.Bottom);
                        Draw.Colour = Colors.White;
                        Draw.Text(vts, screenPos, Vector2.One, HorizontalTextAlign.Left, VerticalTextAlign.Bottom);
                    }
                }
            }
        }
    }

    public readonly struct TimelineConstraintDecorator : IDecorator
    {
        public readonly CharacterAnimation? Animation;

        public TimelineConstraintDecorator(CharacterAnimation? animation)
        {
            Animation = animation;
        }

        public readonly void RenderAfter(in ControlParams p)
        {
            if (Animation == null)
                return;

            var container = p.Instance.Rects.ComputedDrawBounds;
            container.MinY = container.MaxY - 8;
            container.MaxY--;
            container.MaxX--;
            container.MinX++;

            Draw.Colour = Colors.Cyan;

            var hover = AnimationConstraint.AllowAll;

            for (int i = 0; i < Animation.Constraints.Count; i++)
            {
                var constraint = Animation.Constraints[i];
                if (constraint.Constraints == AnimationConstraint.AllowAll)
                    continue;

                ConstraintKeyframe? nextConstraint = (i + 1 == Animation.Constraints.Count) ? null : Animation.Constraints[i + 1];

                float min = constraint.Time / Animation.TotalDuration;
                float max = nextConstraint.HasValue ? (nextConstraint.Value.Time / Animation.TotalDuration) : 1;

                var r = container;
                r.MinX = Utilities.Lerp(container.MinX, container.MaxX, min);
                r.MaxX = Utilities.Lerp(container.MinX, container.MaxX, max);
                if (p.Input.MousePosition.X > r.MinX && p.Input.MousePosition.X < r.MaxX)
                    hover |= constraint.Constraints;
                Draw.Quad(r);

                Draw.Colour.GetHsv(out var h, out var s, out var v);
                Draw.Colour = Color.FromHsv((h + 0.1f) % 1, s, v);
            }

            if (hover != AnimationConstraint.AllowAll)
            {
                var toolTip = new Tooltip(hover.ToString());
                toolTip.RenderAfter(p);
            }
        }

        public readonly void RenderBefore(in ControlParams p)
        {
        }
    }
}
