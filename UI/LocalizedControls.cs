using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TimberbornTrainer.Localization;
using UnityEngine.UIElements;

namespace TimberbornTrainer.UI {
  internal static class LocalizedUI {
    private sealed class Binding { public string Text, Tooltip, Label; public List<string> Choices; }
    private static readonly ConditionalWeakTable<VisualElement, Binding> Data = new ConditionalWeakTable<VisualElement, Binding>();
    private static readonly List<WeakReference<VisualElement>> Elements = new List<WeakReference<VisualElement>>();
    static LocalizedUI() { RuntimeText.Changed += Refresh; }
    private static Binding Get(VisualElement element) {
      if (!Data.TryGetValue(element, out var binding)) { binding = new Binding(); Data.Add(element, binding); Elements.Add(new WeakReference<VisualElement>(element)); }
      return binding;
    }
    internal static void Text(VisualElement element, string raw) { Get(element).Text = raw; Apply(element, Get(element)); }
    internal static void Tooltip(VisualElement element, string raw) { Get(element).Tooltip = raw; Apply(element, Get(element)); }
    internal static void Label(VisualElement element, string raw) { Get(element).Label = raw; Apply(element, Get(element)); }
    internal static void Choices(LocalizedDropdown element, List<string> raw) { Get(element).Choices = raw; Apply(element, Get(element)); }
    internal static void Refresh() {
      for (int i = Elements.Count - 1; i >= 0; --i) {
        if (!Elements[i].TryGetTarget(out var element)) { Elements.RemoveAt(i); continue; }
        if (Data.TryGetValue(element, out var binding)) Apply(element, binding);
      }
    }
    private static void Apply(VisualElement element, Binding binding) {
      if (binding.Text != null) {
        string text = RuntimeText.Render(binding.Text);
        if (element is Label label) label.text = text;
        else if (element is Button button) button.text = text;
        else if (element is Foldout foldout) foldout.text = text;
      }
      if (binding.Tooltip != null) element.tooltip = RuntimeText.Render(binding.Tooltip);
      if (binding.Label != null) {
        string text = RuntimeText.Render(binding.Label);
        // BaseField.label also attaches/removes the label node and updates its USS variant.
        if (element is TextField field) field.label = text;
        else if (element is Toggle toggle) toggle.label = text;
        else if (element is DropdownField dropdown) dropdown.label = text;
      }
      if (binding.Choices != null && element is DropdownField choices) {
        int index = choices.choices == null ? 0 : Math.Max(0, choices.choices.IndexOf(choices.value));
        choices.choices = binding.Choices.Select(RuntimeText.Render).ToList();
        if (index < choices.choices.Count) choices.SetValueWithoutNotify(choices.choices[index]);
      }
    }
    internal static void Release(VisualElement root) {
      for (int i = Elements.Count - 1; i >= 0; --i) {
        if (!Elements[i].TryGetTarget(out var element)) { Elements.RemoveAt(i); continue; }
        var parent = element;
        while (parent != null && !ReferenceEquals(parent, root)) parent = parent.parent;
        if (parent != null) { Data.Remove(element); Elements.RemoveAt(i); }
      }
    }
  }
  internal sealed class LocalizedLabel : Label {
    internal LocalizedLabel(string raw) : base("") { text = raw; }
    public new string text { get => base.text; set => LocalizedUI.Text(this, value); }
    public new string tooltip { get => base.tooltip; set => LocalizedUI.Tooltip(this, value); }
  }
  internal sealed class LocalizedToggle : Toggle {
    internal LocalizedToggle(string raw) : base("") { LocalizedUI.Label(this, raw); }
  }
  internal sealed class LocalizedFoldout : Foldout {
    public new string text { get => base.text; set => LocalizedUI.Text(this, value); }
  }
  internal sealed class LocalizedDropdown : DropdownField {
    internal LocalizedDropdown(string raw, List<string> values, int index) : base("", values.Select(RuntimeText.Render).ToList(), index) { LocalizedUI.Label(this, raw); LocalizedUI.Choices(this, values); }
    public new List<string> choices { get => base.choices; set => LocalizedUI.Choices(this, value); }
    public new string tooltip { get => base.tooltip; set => LocalizedUI.Tooltip(this, value); }
  }
}
