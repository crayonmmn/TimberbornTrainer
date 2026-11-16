using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TimberbornTrainer.Core;
using TimberbornTrainer.Modules;
using TimberbornTrainer.Localization;
using TimberbornTrainer.UI;
using Timberborn.UILayoutSystem;
using Timberborn.SingletonSystem;
using UnityEngine.UIElements;

namespace TimberbornTrainer.Tests {
  public static class RuntimeUiLocalizationTests {
    private static int _checks;
    private static void Check(bool ok, string message) { ++_checks; if (!ok) throw new Exception(message); }
    private sealed class Points : ISciencePoints { public int Value = 100; public int Read() => Value; public void Add(int value) { Value += value; } public void Subtract(int value) { Value -= value; } }
    public static void Run(string languages, string directory) {
      _checks = 0;
      RuntimeText.Configure(languages, Path.Combine(directory, "language.json"), "enUS");
      var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      var session = new TrainerSession { State = state, Store = new ConfigStore(directory) };
      var points = new Points(); var layout = new UILayout(); var catalog = new NativeInventoryCatalog(); var events = new EventBus();
      var editor = new NativeInventoryEditor(catalog, () => state.EffectiveEnabled);
      var panel = new MainPanel(session, layout, new ResourceModule { Science = new ScienceCommands(points, () => state.EffectiveEnabled) }, new BuildingModule(), new BeaverModule(), catalog, editor, events);
      panel.Load(); var root = layout.Root;
      root.Tree().OfType<Button>().Single(b => b.text == "Trainer").Click();
      var language = root.Q<DropdownField>("TrainerLanguage"); Check(language != null, "Actual language entry registered");
      root.Q<Toggle>("TrainerEnabled").value = true;
      var window = (FloatingWindow)typeof(MainPanel).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
      var windowState = window.State; float x = windowState.X, y = windowState.Y; var edge = windowState.Edge; bool hidden = windowState.Hidden;
      // The real window reparents the tab beside the root to avoid clipping.
      var tab = (VisualElement)typeof(FloatingWindow).GetField("_tab", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
      float tabX = tab.style.left, tabY = tab.style.top;
      int subscriptions = events.Count;
      var originalButton = root.Q<Button>("TrainerScienceEntry");
      language.value = "简体中文";
      Check(ReferenceEquals(originalButton, root.Q<Button>("TrainerScienceEntry")) && originalButton.text == "科研点管理", "Text refresh without rebuilding controls");
      Check(state.EffectiveEnabled && points.Value == 100 && events.Count == subscriptions, "No gameplay writes or duplicate native subscriptions");
      Check(ReferenceEquals(windowState, window.State) && x == windowState.X && y == windowState.Y && edge == windowState.Edge && hidden == windowState.Hidden, "Window state unchanged");
      Check(tabX == tab.style.left && tabY == tab.style.top, "Independent tab position unchanged");
      root.Q<Button>("TrainerScienceEntry").Click();
      root.Q<TextField>("ScienceAddInput").value = "10"; root.Q<Button>("ScienceAddButton").Click();
      var status = root.Q<Label>("TrainerStatus"); Check(points.Value == 110 && status.text.Contains("成功"), "Actual science result Chinese");
      language.value = "English";
      Check(points.Value == 110 && status.text.Contains("Success") && !status.text.Contains("成功"), "Cached operation result changes language without repeating operation");
      root.Q<TextField>("ScienceAddInput").value = "-1"; root.Q<Button>("ScienceAddButton").Click();
      Check(status.text.Contains("integer") || status.text.Contains("Integer"), "Actual validation message English");
      for (int i = 0; i < 10; ++i) { language.value = "简体中文"; language.value = "English"; }
      Check(points.Value == 110 && state.EffectiveEnabled && events.Count == 0, "Repeated language switches do not write or subscribe");
      Check(root.Q<TextField>("ScienceAddInput").value == "-1", "Text input preserved");
      panel.Unload(); RuntimeText.Select("zh-CN"); Check(events.Count == 0, "Unload releases localized controls and native subscriptions");
      Console.WriteLine("Runtime localization actual UI adapters: " + _checks + " passed (UI substitutes, not real game).");
    }
  }
}
