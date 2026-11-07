using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberbornTrainer.UI {
  internal static class ControlStyles {
    internal static readonly Color Light = new Color(0.94f, 0.96f, 0.94f);
    internal static readonly Color Accent = new Color(0.43f, 0.85f, 0.68f);
    internal static readonly Color Muted = new Color(0.67f, 0.73f, 0.71f);
    internal static readonly Color Error = new Color(1f, 0.65f, 0.58f);
    internal static void Border(VisualElement e, Color color) {
      e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = 1;
      e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = color;
    }
    internal static Label Text(string text) {
      var label = new Label(text);
      label.style.color = Light;
      label.style.whiteSpace = WhiteSpace.Normal;
      label.style.marginTop = label.style.marginBottom = 3;
      return label;
    }
    internal static TextField Input(string label, string value) {
      var field = new TextField(label) { value = value };
      field.style.color = Light;
      field.style.minHeight = 30;
      field.style.flexGrow = 1;
      field.labelElement.style.color = Light;
      field.labelElement.style.minWidth = 100;
      var inner = field.Q("unity-text-input") ?? field;
      inner.style.color = Light;
      inner.style.backgroundColor = new Color(0.07f, 0.1f, 0.1f);
      inner.style.paddingLeft = 6;
      Border(inner, Muted);
      field.RegisterCallback<FocusInEvent>(_ => Border(inner, Accent));
      field.RegisterCallback<FocusOutEvent>(_ => Border(inner, Muted));
      return field;
    }
    internal static VisualElement Row() {
      var row = new VisualElement();
      row.style.flexDirection = FlexDirection.Row;
      row.style.alignItems = Align.Center;
      row.style.marginTop = 4;
      return row;
    }
  }

  internal sealed class ActionButton : Button {
    private bool _hover, _pressed, _focused;
    internal ActionButton(string text, Action action) : base(action) {
      this.text = text;
      style.minHeight = 30;
      style.paddingLeft = style.paddingRight = 8;
      style.marginRight = 4;
      style.marginTop = 3;
      RegisterCallback<MouseEnterEvent>(_ => { _hover = true; Paint(); });
      RegisterCallback<MouseLeaveEvent>(_ => { _hover = false; _pressed = false; Paint(); });
      RegisterCallback<MouseDownEvent>(_ => { _pressed = true; Paint(); });
      RegisterCallback<MouseUpEvent>(_ => { _pressed = false; Paint(); });
      RegisterCallback<MouseCaptureOutEvent>(_ => { _pressed = false; Paint(); });
      RegisterCallback<FocusInEvent>(_ => { _focused = true; Paint(); });
      RegisterCallback<FocusOutEvent>(_ => { _focused = false; Paint(); });
      Paint();
    }
    internal void Available(bool enabled) { SetEnabled(enabled); Paint(); }
    private void Paint() {
      style.color = enabledInHierarchy ? ControlStyles.Light : ControlStyles.Muted;
      style.backgroundColor = !enabledInHierarchy ? new Color(.16f, .18f, .18f)
        : _pressed ? new Color(.10f, .34f, .29f) : _hover ? new Color(.22f, .39f, .34f) : new Color(.15f, .25f, .23f);
      ControlStyles.Border(this, _focused || _hover ? ControlStyles.Accent : ControlStyles.Muted);
    }
  }
}
