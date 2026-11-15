using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberbornTrainer.UI {
  // Local mouse events only. Never registers a game input processor or blocks keyboard input.
  internal sealed class FloatingWindow : IDisposable {
    private readonly VisualElement _root, _panel, _title, _titleText;
    private readonly Label _tab;
    private readonly EdgeTabGesture _tabGesture = new EdgeTabGesture();
    private float _tabWakeAt = -1;
    private bool _tabHovered, _suppressTabHover, _rootHovered;
    private readonly List<DropdownField> _dropdowns = new List<DropdownField>();
    private VisualElement _viewport;
    private readonly IVisualElementScheduledItem _pump;
    internal readonly FloatingWindowState State = new FloatingWindowState();
    private float _originX, _originY, _lastTime, _scrollUntil;
    private bool _pressed, _disposed;
    internal FloatingWindow(VisualElement root, VisualElement panel, VisualElement title, VisualElement titleText) {
      _root = root; _panel = panel; _title = title; _titleText = titleText;
      _root.pickingMode = PickingMode.Ignore; _root.focusable = false;
      _tab = new Label("修改\n工具") { name = "TrainerEdgeTab", tooltip = "单击或悬停展开；拖动仅移动标签" };
      _tab.style.position = Position.Absolute; _tab.style.width = FloatingWindowState.TabWidth;
      _tab.style.height = 70; _tab.style.top = 35; _tab.style.fontSize = 14;
      _tab.style.color = ControlStyles.Light; _tab.style.backgroundColor = new Color(.10f, .25f, .21f);
      _tab.style.unityTextAlign = TextAnchor.MiddleCenter; ControlStyles.Border(_tab, ControlStyles.Accent);
      _root.Add(_tab);
      PassiveFocus(_root);
      _root.RegisterCallback<MouseEnterEvent>(Enter); _root.RegisterCallback<MouseLeaveEvent>(Leave);
      _tab.RegisterCallback<MouseEnterEvent>(TabEnter); _tab.RegisterCallback<MouseLeaveEvent>(TabLeave);
      _tab.RegisterCallback<MouseDownEvent>(TabDown); _tab.RegisterCallback<MouseMoveEvent>(TabMove);
      _tab.RegisterCallback<MouseUpEvent>(TabUp); _tab.RegisterCallback<MouseCaptureOutEvent>(TabCaptureLost);
      _root.RegisterCallback<MouseDownEvent>(Down, TrickleDown.TrickleDown);
      _title.RegisterCallback<MouseDownEvent>(StartDrag);
      _root.RegisterCallback<MouseMoveEvent>(Move); _root.RegisterCallback<MouseUpEvent>(Up, TrickleDown.TrickleDown);
      _root.RegisterCallback<MouseCaptureOutEvent>(CaptureLost);
      _root.RegisterCallback<WheelEvent>(Wheel, TrickleDown.TrickleDown);
      _root.RegisterCallback<FocusInEvent>(FocusIn); _root.RegisterCallback<FocusOutEvent>(FocusOut);
      _root.RegisterCallback<GeometryChangedEvent>(Geometry);
      _root.RegisterCallback<AttachToPanelEvent>(Attach); _root.RegisterCallback<DetachFromPanelEvent>(Detach);
      _pump = _root.schedule.Execute(Pump).Every(16); _pump.Pause();
      BindViewport(); Paint();
    }
    private static bool KeyboardControl(VisualElement element) {
      for (var p = element; p != null; p = p.parent) if (p is TextField || p is DropdownField) return true;
      return false;
    }
    private void PassiveFocus(VisualElement element) {
      if (!KeyboardControl(element)) element.focusable = false;
      if (element is DropdownField dropdown) { dropdown.RegisterCallback<ChangeEvent<string>>(DropdownChanged); _dropdowns.Add(dropdown); }
      foreach (var child in element.Children()) PassiveFocus(child);
    }
    private void DropdownChanged(ChangeEvent<string> e) { (e.target as VisualElement)?.Blur(); State.Edit(false, Time.unscaledTime); WakePump(); }
    private Vector2 Local(Vector2 point) {
      var p = _root.parent == null ? point : _root.parent.WorldToLocal(point);
      return new Vector2(p.x - _originX, p.y - _originY);
    }
    private void BindViewport() {
      if (_root.panel == null || _viewport != null) return;
      _viewport = _root.panel.visualTree;
      // A sibling overlay can move anywhere in the viewport without moving/clipping with the hidden window.
      if (_root.parent != null) _root.parent.Add(_tab);
      _viewport.RegisterCallback<GeometryChangedEvent>(Geometry);
      Measure();
    }
    private void Measure() {
      if (_viewport == null) return;
      var bounds = _viewport.worldBound;
      var min = _root.parent == null ? bounds.min : _root.parent.WorldToLocal(bounds.min);
      var max = _root.parent == null ? bounds.max : _root.parent.WorldToLocal(bounds.max);
      float vw = max.x - min.x, vh = max.y - min.y;
      if (vw <= 0 || vh <= 0 || float.IsNaN(vw) || float.IsNaN(vh)) return;
      _originX = min.x; _originY = min.y;
      float width = Math.Min(600, vw), height = _root.resolvedStyle.height;
      if (float.IsNaN(height) || height <= 0) height = 650;
      _root.style.width = width; _panel.style.width = width;
      _panel.style.maxHeight = vh;
      State.Resize(vw, vh, width, height, Time.unscaledTime);
    }
    internal void SetOpen(bool open) {
      if (_disposed) return;
      CancelTab();
      State.SetOpen(open, Time.unscaledTime); _pressed = false; _scrollUntil = 0;
      if (!open) { (_root.panel?.focusController.focusedElement as VisualElement)?.Blur(); if (_root.HasMouseCapture()) _root.ReleaseMouse(); }
      Measure(); Paint(); WakePump();
    }
    private void Enter(MouseEnterEvent e) {
      if (e.target != _root && e.target != _tab) return;
      _rootHovered = true;
      if (_tabGesture.Active) return;
      State.Hover(true, Time.unscaledTime); WakePump();
    }
    private void Leave(MouseLeaveEvent e) {
      if (e.target != _root) return;
      _rootHovered = false;
      State.Hover(_tabHovered && _tabWakeAt < 0 && !_suppressTabHover && !_tabGesture.Active, Time.unscaledTime);
      var captured = _root.panel?.GetCapturingElement(PointerId.mousePointerId) as VisualElement;
      bool ownCapture = false;
      for (var p = captured; p != null; p = p.parent) if (p == _root) { ownCapture = true; break; }
      // A plain label does not capture the release outside this window; don't leave a stuck interaction.
      if (_pressed && !State.Dragging && !ownCapture) { _pressed = false; State.Interaction(false, Time.unscaledTime); }
      WakePump();
    }
    private void TabEnter(MouseEnterEvent e) {
      if (_disposed || e.target != _tab) return;
      _tabHovered = true;
      if (_tabGesture.Active || _suppressTabHover) return;
      _tabWakeAt = Time.unscaledTime + .2f; WakePump();
    }
    private void TabLeave(MouseLeaveEvent e) {
      if (e.target != _tab) return;
      _tabHovered = false; _tabWakeAt = -1;
      if (!_tabGesture.Active) {
        _suppressTabHover = false;
        if (!_rootHovered) State.Hover(false, Time.unscaledTime);
      }
      WakePump();
    }
    private void TabDown(MouseDownEvent e) {
      if (_disposed || e.button != 0 || _tabGesture.Active || State.Dragging || !State.Open || State.Edge == DockEdge.None) return;
      _tabWakeAt = -1; _suppressTabHover = true;
      State.HoldHiddenForTab();
      var p = Local(e.mousePosition); _tabGesture.Press(p.x, p.y);
      _tab.CaptureMouse(); e.StopPropagation(); Paint(); WakePump();
    }
    private void TabMove(MouseMoveEvent e) {
      if (!_tabGesture.Active) return;
      var p = Local(e.mousePosition); _tabGesture.Move(p.x, p.y); Paint(); e.StopPropagation();
    }
    private void TabUp(MouseUpEvent e) {
      if (e.button != 0 || !_tabGesture.Active) return;
      var p = Local(e.mousePosition); bool click = _tabGesture.Release(p.x, p.y);
      // Keep suppression through layout-generated enter events after release, until an actual leave.
      _tabWakeAt = -1; _suppressTabHover = !click;
      if (_tab.HasMouseCapture()) _tab.ReleaseMouse();
      if (click) {
        State.Hover(true, Time.unscaledTime);
        if (!_tabHovered && !_rootHovered) State.Hover(false, Time.unscaledTime);
      }
      e.StopPropagation(); Paint(); WakePump();
    }
    private void TabCaptureLost(MouseCaptureOutEvent e) {
      if (!_tabGesture.Active) return;
      _tabGesture.Cancel(); _tabWakeAt = -1; _suppressTabHover = true;
      Paint(); WakePump();
    }
    private void CancelTab() {
      _tabGesture.Cancel(); _tabWakeAt = -1; _tabHovered = _suppressTabHover = false;
      if (_tab.HasMouseCapture()) _tab.ReleaseMouse();
    }
    private void Down(MouseDownEvent e) {
      if (e.button != 0) return;
      _pressed = true; State.Interaction(true, Time.unscaledTime);
      if (!KeyboardControl(e.target as VisualElement)) (_root.panel?.focusController.focusedElement as VisualElement)?.Blur();
    }
    private void StartDrag(MouseDownEvent e) {
      if (e.button != 0 || (e.target != _title && e.target != _titleText && e.target != _tab)) return;
      var p = Local(e.mousePosition); State.BeginDrag(p.x, p.y);
      _root.CaptureMouse(); e.StopPropagation(); WakePump();
    }
    private void Move(MouseMoveEvent e) {
      if (!State.Dragging) return;
      var p = Local(e.mousePosition); State.Drag(p.x, p.y); Paint(); e.StopPropagation();
    }
    private void Up(MouseUpEvent e) {
      if (e.button != 0) return;
      bool dragging = State.Dragging;
      _pressed = false; State.EndDrag(Time.unscaledTime); State.Interaction(false, Time.unscaledTime);
      var p = Local(e.mousePosition);
      State.Hover(p.x >= State.X && p.x <= State.X + State.Width && p.y >= State.Y && p.y <= State.Y + State.Height, Time.unscaledTime);
      if (_root.HasMouseCapture()) _root.ReleaseMouse();
      if (dragging) e.StopPropagation();
      Paint(); WakePump();
    }
    private void CaptureLost(MouseCaptureOutEvent e) {
      if (State.Dragging && e.target != _root) return;
      _pressed = false; State.EndDrag(Time.unscaledTime); State.Interaction(false, Time.unscaledTime); Paint(); WakePump();
    }
    private void Wheel(WheelEvent e) { _scrollUntil = Time.unscaledTime + .2f; State.Interaction(true, Time.unscaledTime); WakePump(); }
    private void FocusIn(FocusInEvent e) { if (KeyboardControl(e.target as VisualElement)) State.Edit(true, Time.unscaledTime); WakePump(); }
    private void FocusOut(FocusOutEvent e) { if (KeyboardControl(e.target as VisualElement)) State.Edit(false, Time.unscaledTime); WakePump(); }
    private void Geometry(GeometryChangedEvent e) {
      if (e.target != _root && e.target != _viewport) return;
      if (e.oldRect.width == e.newRect.width && e.oldRect.height == e.newRect.height) return;
      Measure(); Paint(); WakePump();
    }
    private void Attach(AttachToPanelEvent e) {
      if (e.target != _root) return;
      CancelTab(); _tab.style.display = DisplayStyle.None;
      State.SetOpen(_panel.style.display.value != DisplayStyle.None, Time.unscaledTime); BindViewport(); Paint(); WakePump();
    }
    private void Detach(DetachFromPanelEvent e) {
      if (e.target != _root) return;
      CancelTab(); _tab.style.display = DisplayStyle.None;
      _tab.RemoveFromHierarchy();
      _pump.Pause(); State.SetOpen(false, Time.unscaledTime);
      if (_viewport != null) _viewport.UnregisterCallback<GeometryChangedEvent>(Geometry);
      _viewport = null;
    }
    private void WakePump() { if (_disposed) return; _lastTime = Time.unscaledTime; _pump.Resume(); }
    internal void Pump() {
      if (_disposed) return;
      float now = Time.unscaledTime;
      if (_tabWakeAt >= 0 && now >= _tabWakeAt) {
        _tabWakeAt = -1;
        if (_tabHovered && !_tabGesture.Active && !_suppressTabHover) State.Hover(true, now);
      }
      if (_scrollUntil > 0 && now >= _scrollUntil) { _scrollUntil = 0; if (!_pressed) State.Interaction(false, now); }
      if (!_tabGesture.Active) State.Advance(now, now - _lastTime);
      _lastTime = now; Paint();
      if (!State.NeedsUpdate && _scrollUntil == 0 && _tabWakeAt < 0) _pump.Pause();
    }
    private void Paint() {
      _root.style.left = _originX + State.X; _root.style.top = _originY + State.Y;
      bool tab = State.Open && State.Edge != DockEdge.None && (State.Hidden || State.Animating);
      _tab.style.display = tab ? DisplayStyle.Flex : DisplayStyle.None;
      _tabGesture.Layout(State.ViewWidth, State.ViewHeight,
        State.Edge == DockEdge.Left ? 0 : State.ViewWidth - FloatingWindowState.TabWidth, State.Y + 35);
      bool child = _tab.parent == _root;
      _tab.style.left = (child ? -State.X : _originX) + _tabGesture.X;
      _tab.style.top = (child ? -State.Y : _originY) + _tabGesture.Y;
      _tab.style.width = _tabGesture.Width; _tab.style.height = _tabGesture.Height;
    }
    public void Dispose() {
      if (_disposed) return; _disposed = true; _pump.Pause();
      CancelTab();
      if (_root.HasMouseCapture()) _root.ReleaseMouse();
      if (_viewport != null) _viewport.UnregisterCallback<GeometryChangedEvent>(Geometry);
      _root.UnregisterCallback<MouseEnterEvent>(Enter); _root.UnregisterCallback<MouseLeaveEvent>(Leave);
      _tab.UnregisterCallback<MouseEnterEvent>(TabEnter); _tab.UnregisterCallback<MouseLeaveEvent>(TabLeave);
      _tab.UnregisterCallback<MouseDownEvent>(TabDown); _tab.UnregisterCallback<MouseMoveEvent>(TabMove);
      _tab.UnregisterCallback<MouseUpEvent>(TabUp); _tab.UnregisterCallback<MouseCaptureOutEvent>(TabCaptureLost);
      _root.UnregisterCallback<MouseDownEvent>(Down, TrickleDown.TrickleDown);
      _title.UnregisterCallback<MouseDownEvent>(StartDrag);
      _root.UnregisterCallback<MouseMoveEvent>(Move); _root.UnregisterCallback<MouseUpEvent>(Up, TrickleDown.TrickleDown);
      _root.UnregisterCallback<MouseCaptureOutEvent>(CaptureLost); _root.UnregisterCallback<WheelEvent>(Wheel, TrickleDown.TrickleDown);
      _root.UnregisterCallback<FocusInEvent>(FocusIn); _root.UnregisterCallback<FocusOutEvent>(FocusOut);
      _root.UnregisterCallback<GeometryChangedEvent>(Geometry); _root.UnregisterCallback<AttachToPanelEvent>(Attach);
      _root.UnregisterCallback<DetachFromPanelEvent>(Detach); _tab.RemoveFromHierarchy(); _viewport = null;
      foreach (var dropdown in _dropdowns) dropdown.UnregisterCallback<ChangeEvent<string>>(DropdownChanged);
      _dropdowns.Clear();
    }
  }
}
