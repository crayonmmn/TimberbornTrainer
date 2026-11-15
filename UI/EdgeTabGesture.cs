using System;

namespace TimberbornTrainer.UI {
  internal enum TabPointerState { Idle, Pressed, Dragging }
  // Only the label position, in UI panel coordinates. Never moves the full window or saves state.
  internal sealed class EdgeTabGesture {
    internal const float Threshold = 6;
    internal TabPointerState Phase { get; private set; }
    internal bool Active => Phase != TabPointerState.Idle;
    internal bool CustomPosition { get; private set; }
    internal float X { get; private set; }
    internal float Y { get; private set; }
    internal float Width { get; private set; } = 36;
    internal float Height { get; private set; } = 70;
    private float _maxX, _maxY, _downX, _downY, _startX, _startY;
    private static float Clamp(float v, float max) => Math.Max(0, Math.Min(v, max));
    internal void Layout(float vw, float vh, float anchorX, float anchorY) {
      Width = Math.Min(36, vw); Height = Math.Min(70, vh);
      _maxX = Math.Max(0, vw - Width); _maxY = Math.Max(0, vh - Height);
      if (!CustomPosition && !Active) { X = anchorX; Y = anchorY; }
      X = Clamp(X, _maxX); Y = Clamp(Y, _maxY);
    }
    internal void Press(float x, float y) {
      Phase = TabPointerState.Pressed; _downX = x; _downY = y; _startX = X; _startY = Y;
    }
    internal void Move(float x, float y) {
      if (!Active) return;
      float dx = x - _downX, dy = y - _downY;
      if (Phase == TabPointerState.Pressed && dx * dx + dy * dy > Threshold * Threshold)
        Phase = TabPointerState.Dragging;
      if (Phase != TabPointerState.Dragging) return;
      CustomPosition = true; X = Clamp(_startX + dx, _maxX); Y = Clamp(_startY + dy, _maxY);
    }
    internal bool Release(float x, float y) {
      if (!Active) return false;
      Move(x, y); bool click = Phase == TabPointerState.Pressed; Phase = TabPointerState.Idle; return click;
    }
    internal void Cancel() { Phase = TabPointerState.Idle; }
  }
}
