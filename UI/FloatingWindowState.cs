using System;

namespace TimberbornTrainer.UI {
  internal enum DockEdge { None, Left, Right }
  // Coordinates are panel units. No input polling, game services or persisted settings.
  internal sealed class FloatingWindowState {
    internal const float TabWidth = 36, SnapDistance = 28, HideDelay = .5f;
    internal float X { get; private set; } = 260;
    internal float Y { get; private set; } = 105;
    internal float Width { get; private set; } = 600;
    internal float Height { get; private set; } = 650;
    internal float ViewWidth { get; private set; } = 1920;
    internal float ViewHeight { get; private set; } = 1080;
    internal DockEdge Edge { get; private set; }
    internal bool Open { get; private set; }
    internal bool Dragging { get; private set; }
    internal bool Hovered { get; private set; }
    internal bool Interacting { get; private set; }
    internal bool Editing { get; private set; }
    internal bool Hidden { get; private set; }
    private float _offsetX, _offsetY, _hideAt;
    private bool _partialDrag;
    private float ExpandedX => Edge == DockEdge.Left ? 0 : Edge == DockEdge.Right ? ViewWidth - Width : X;
    private float TargetX => Hidden ? (Edge == DockEdge.Left ? -Width + Math.Min(TabWidth, Width) : ViewWidth - Math.Min(TabWidth, Width)) : ExpandedX;
    internal bool Animating => !Dragging && Math.Abs(X - TargetX) > .1f;
    private bool CanHide => Open && Edge != DockEdge.None && !Hovered && !Interacting && !Editing && !Dragging;
    internal bool NeedsUpdate => Animating || (CanHide && !Hidden);
    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(value, Math.Max(min, max)));
    internal void Resize(float vw, float vh, float width, float height, float now) {
      if (!Finite(vw) || !Finite(vh) || !Finite(width) || !Finite(height) || vw <= 0 || vh <= 0 || width <= 0 || height <= 0) return;
      ViewWidth = vw; ViewHeight = vh; Width = Math.Min(width, vw); Height = Math.Min(height, vh);
      Y = Clamp(Y, 0, vh - Height);
      X = Hidden && Edge != DockEdge.None ? TargetX : Edge != DockEdge.None ? ExpandedX : Clamp(X, 0, vw - Width);
      _hideAt = now + HideDelay;
    }
    internal void SetOpen(bool open, float now) {
      Open = open; Hidden = false; Interacting = Editing = false;
      if (Dragging) EndDrag(now);
      X = ExpandedX; _hideAt = now + HideDelay;
    }
    internal void Hover(bool value, float now) { Hovered = value; if (value) Hidden = false; _hideAt = now + HideDelay; }
    internal void Interaction(bool value, float now) { Interacting = value; if (value) Hidden = false; _hideAt = now + HideDelay; }
    internal void Edit(bool value, float now) { Editing = value; if (value) Hidden = false; _hideAt = now + HideDelay; }
    // A label gesture must not continue a hover reveal or move the hidden window along with the label.
    internal void HoldHiddenForTab() {
      if (!Open || Edge == DockEdge.None) return;
      Hovered = false; Hidden = true; X = TargetX;
    }
    internal void BeginDrag(float px, float py) {
      _offsetX = px - X; _offsetY = py - Y;
      _partialDrag = X < 0 || X > ViewWidth - Width;
      Hidden = false; Dragging = true; Edge = DockEdge.None;
    }
    internal void Drag(float px, float py) {
      if (!Dragging) return;
      X = Clamp(px - _offsetX, _partialDrag ? -Width + Math.Min(TabWidth, Width) : 0,
        _partialDrag ? ViewWidth - Math.Min(TabWidth, Width) : ViewWidth - Width);
      Y = Clamp(py - _offsetY, 0, ViewHeight - Height);
    }
    internal void EndDrag(float now) {
      if (!Dragging) return;
      Dragging = false; Interacting = false;
      if (X <= SnapDistance) Edge = DockEdge.Left;
      else if (X >= ViewWidth - Width - SnapDistance) Edge = DockEdge.Right;
      else Edge = DockEdge.None;
      Hidden = false; X = Edge == DockEdge.None ? Clamp(X, 0, ViewWidth - Width) : ExpandedX;
      _hideAt = now + HideDelay;
    }
    internal void Advance(float now, float elapsed) {
      if (Dragging) return;
      if (CanHide && !Hidden && !Animating && now >= _hideAt) Hidden = true;
      if (!CanHide && (Hovered || Editing || Interacting)) Hidden = false;
      float target = TargetX;
      float factor = 1 - (float)Math.Exp(-18 * Clamp(elapsed, 0, .05f));
      X += (target - X) * factor;
      if (Math.Abs(X - target) < .1f) X = target;
      // A reveal must finish before a fresh idle delay can start.
      if (!Hidden && !Animating && _hideAt < now) _hideAt = now + HideDelay;
    }
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
  }
}
