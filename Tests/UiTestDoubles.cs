// Headless UI event/tree substitute only: no Unity layout/rendering or native scene execution.
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine {
  public struct Color { public Color(float r, float g, float b, float a = 1) { } }
  public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 zero => default; }
  public struct Rect {
    public float x,y,width,height; public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
    public Vector2 min=>new Vector2(x,y); public Vector2 max=>new Vector2(x+width,y+height);
  }
  public enum TextAnchor { MiddleCenter }
  public static class Time { public static float unscaledTime; }
}
namespace UnityEngine.UIElements {
  public enum DisplayStyle { Flex, None }
  public enum Position { Absolute }
  public enum WhiteSpace { Normal }
  public enum FlexDirection { Row }
  public enum Align { Center }
  public enum PickingMode { Position, Ignore }
  public enum TrickleDown { NoTrickleDown, TrickleDown }
  public struct StyleEnum<T> { public T value; public static implicit operator StyleEnum<T>(T value) => new StyleEnum<T> { value = value }; }
  public sealed class Style {
    public StyleEnum<DisplayStyle> display;
    public Position position; public WhiteSpace whiteSpace; public FlexDirection flexDirection; public Align alignItems;
    public float left, top, width, height, maxWidth, minWidth, maxHeight, minHeight, fontSize, flexGrow, flexBasis, flexShrink,
      paddingLeft, paddingRight, paddingTop, paddingBottom, marginTop, marginBottom, marginRight,
      borderLeftWidth, borderRightWidth, borderTopWidth, borderBottomWidth;
    public UnityEngine.Color color, backgroundColor, borderLeftColor, borderRightColor, borderTopColor, borderBottomColor;
    public UnityEngine.TextAnchor unityTextAlign;
  }
  public interface IVisualElementScheduledItem { IVisualElementScheduledItem Every(long interval); void Pause(); void Resume(); }
  public sealed class ScheduledItem : IVisualElementScheduledItem {
    public bool Paused; public readonly Action Action; public ScheduledItem(Action a){Action=a;}
    public IVisualElementScheduledItem Every(long interval)=>this; public void Pause(){Paused=true;} public void Resume(){Paused=false;}
  }
  public sealed class Scheduler { public readonly List<ScheduledItem> Items=new List<ScheduledItem>(); public IVisualElementScheduledItem Execute(Action a){var i=new ScheduledItem(a);Items.Add(i);return i;} }
  public sealed class FocusController { public object focusedElement; }
  public static class PointerId { public const int mousePointerId=0; }
  public sealed class TestPanel { public VisualElement visualTree; public readonly FocusController focusController=new FocusController(); public object GetCapturingElement(int id)=>VisualElement.Captured; }
  public class VisualElement {
    public string name, tooltip; public readonly Style style = new Style();
    public VisualElement parent; public readonly List<VisualElement> ChildItems = new List<VisualElement>();
    public bool enabledSelf = true, Attached;
    private TestPanel _testPanel;
    public TestPanel panel => parent?.panel ?? (Attached ? (_testPanel ?? (_testPanel=new TestPanel{visualTree=this})) : null);
    public bool focusable=true; public PickingMode pickingMode;
    public readonly Scheduler schedule=new Scheduler();
    public float LayoutWidth=600,LayoutHeight=650;
    public UnityEngine.Rect worldBound=>new UnityEngine.Rect(style.left,style.top,style.width>0?style.width:LayoutWidth,LayoutHeight);
    public UnityEngine.Rect resolvedStyle=>worldBound;
    public UnityEngine.Vector2 WorldToLocal(UnityEngine.Vector2 p)=>new UnityEngine.Vector2(p.x-worldBound.x,p.y-worldBound.y);
    private static VisualElement _capture;
    public static VisualElement Captured=>_capture;
    public bool HasMouseCapture()=>ReferenceEquals(_capture,this);
    public void CaptureMouse(){_capture=this;}
    public void ReleaseMouse(){if(HasMouseCapture()){_capture=null;Dispatch(new MouseCaptureOutEvent());}}
    public void Focus(){if(panel==null)return;panel.focusController.focusedElement=this;Dispatch(new FocusInEvent());}
    public void Blur(){if(panel!=null&&ReferenceEquals(panel.focusController.focusedElement,this)){panel.focusController.focusedElement=null;Dispatch(new FocusOutEvent());}}
    public bool enabledInHierarchy => enabledSelf && (parent == null || parent.enabledInHierarchy);
    public bool Visible => style.display.value != DisplayStyle.None && (parent == null || parent.Visible);
    private sealed class Handler { public Delegate Callback; public TrickleDown Phase; }
    private readonly Dictionary<Type, List<Handler>> _callbacks = new Dictionary<Type, List<Handler>>();
    public IEnumerable<VisualElement> Children()=>ChildItems;
    public void Add(VisualElement e) { e.RemoveFromHierarchy(); ChildItems.Add(e); e.parent = this; }
    public void Insert(int i, VisualElement e) { e.RemoveFromHierarchy(); ChildItems.Insert(i, e); e.parent = this; }
    public void Clear() { foreach (var e in ChildItems) e.parent = null; ChildItems.Clear(); }
    public void RemoveFromHierarchy() { parent?.ChildItems.Remove(this); parent = null; }
    public IEnumerable<VisualElement> Tree() { yield return this; foreach (var c in ChildItems) foreach (var e in c.Tree()) yield return e; }
    public T Q<T>(string name = null, string className = null) where T : VisualElement => Tree().OfType<T>().FirstOrDefault(e => name == null || e.name == name);
    public VisualElement Q(string name = null, string className = null) => name == null ? null : Tree().FirstOrDefault(e => e.name == name);
    public void SetEnabled(bool enabled) { enabledSelf = enabled; }
    public void RegisterCallback<T>(Action<T> action,TrickleDown phase=TrickleDown.NoTrickleDown) {
      if (!_callbacks.TryGetValue(typeof(T), out var list)) _callbacks.Add(typeof(T), list = new List<Handler>());
      list.Add(new Handler{Callback=action,Phase=phase});
    }
    public void UnregisterCallback<T>(Action<T> action,TrickleDown phase=TrickleDown.NoTrickleDown){if(_callbacks.TryGetValue(typeof(T),out var list))list.RemoveAll(h=>h.Callback.Equals(action)&&h.Phase==phase);}
    protected void Send<T>(T e) {if(e is EventBase ev&&ev.target==null)ev.target=this; if (_callbacks.TryGetValue(typeof(T), out var list)) foreach (var d in list.ToArray()) ((Action<T>)d.Callback)(e); }
    private void Deliver<T>(T e,TrickleDown phase) where T:EventBase {if(_callbacks.TryGetValue(typeof(T),out var list))foreach(var h in list.ToArray()){if(e.Stopped)return;if(h.Phase==phase)((Action<T>)h.Callback)(e);}}
    public void Dispatch<T>(T e) where T:EventBase {
      var target=(e is MouseMoveEvent||e is MouseUpEvent)&&_capture!=null?_capture:this;e.target=target;
      var path=new List<VisualElement>();for(var p=target;p!=null;p=p.parent)path.Add(p);
      for(int i=path.Count-1;i>=0;--i)path[i].Deliver(e,TrickleDown.TrickleDown);
      foreach(var p in path)p.Deliver(e,TrickleDown.NoTrickleDown);
    }
  }
  public class Label : VisualElement { public string text; public Label(string text) { this.text = text; } }
  public class Button : VisualElement {
    public string text; private readonly Action _action;
    public Button(Action action) { _action = action; }
    public void Click(int count = 1) { if (Visible && enabledInHierarchy) { _action(); Send(new ClickEvent { clickCount = count }); } }
  }
  public class ChangeEvent<T>:EventBase { public T newValue; }
  public class ValueField<T> : VisualElement {
    protected T _value; public readonly Label labelElement = new Label("");
    public T value { get => _value; set { _value = value; Send(new ChangeEvent<T> { newValue = value }); } }
    public void SetValueWithoutNotify(T v) { _value = v; }
    public void RegisterValueChangedCallback(Action<ChangeEvent<T>> callback) { RegisterCallback(callback); }
  }
  public class TextField : ValueField<string> { public TextField(string label) { labelElement.text = label; } }
  public class Toggle : ValueField<bool> { public Toggle(string label = "") { labelElement.text = label; } }
  public class DropdownField : ValueField<string> {
    public List<string> choices;
    public int index { get => choices.IndexOf(value); set => this.value = choices[value]; }
    public DropdownField(string label, List<string> choices, int index) { this.choices = choices; labelElement.text = label; SetValueWithoutNotify(choices[index]); }
  }
  public class Foldout : ValueField<bool> { public string text; public Foldout() { Add(new Toggle()); } }
  public class ScrollView : VisualElement { public UnityEngine.Vector2 scrollOffset; }
  public class ClickEvent { public int clickCount; }
  public class EventBase { public object target; public bool Stopped; public void StopPropagation(){Stopped=true;} }
  public class MouseEnterEvent:EventBase { } public class MouseLeaveEvent:EventBase { }
  public class MouseDownEvent:EventBase { public int button; public UnityEngine.Vector2 mousePosition; }
  public class MouseUpEvent:EventBase { public int button; public UnityEngine.Vector2 mousePosition; }
  public class MouseMoveEvent:EventBase { public UnityEngine.Vector2 mousePosition; }
  public class MouseCaptureOutEvent:EventBase { } public class FocusInEvent:EventBase { } public class FocusOutEvent:EventBase { }
  public class WheelEvent:EventBase { }
  public class KeyDownEvent:EventBase { public char character; }
  public class Slider:ValueField<float> { }
  public class GeometryChangedEvent:EventBase { public UnityEngine.Rect oldRect,newRect; }
  public class AttachToPanelEvent:EventBase { } public class DetachFromPanelEvent:EventBase { }
}
namespace Timberborn.SingletonSystem {
  [AttributeUsage(AttributeTargets.Method)] public sealed class OnEventAttribute : Attribute { }
  public sealed class EventBus {
    private readonly HashSet<object> _listeners = new HashSet<object>(); public int Count => _listeners.Count;
    public void Register(object o) { _listeners.Add(o); } public void Unregister(object o) { _listeners.Remove(o); }
    public void Post(object e) {
      foreach (var o in _listeners.ToArray()) foreach (var m in o.GetType().GetMethods())
        if (Attribute.IsDefined(m, typeof(OnEventAttribute)) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == e.GetType()) m.Invoke(o, new[] { e });
    }
  }
  public interface ILoadableSingleton { void Load(); } public interface IUnloadableSingleton { void Unload(); }
  public interface IUpdatableSingleton { void UpdateSingleton(); }
}
namespace Timberborn.SelectionSystem {
  public sealed class SelectableObjectSelectedEvent { } public sealed class SelectableObjectUnselectedEvent { }
}
namespace Timberborn.UILayoutSystem {
  public sealed class UILayout {
    public UnityEngine.UIElements.VisualElement Root;
    public void AddAbsoluteItem(UnityEngine.UIElements.VisualElement e) {
      var screen=new UnityEngine.UIElements.VisualElement{Attached=true,LayoutWidth=1920,LayoutHeight=1080};screen.Add(e);Root=e;
    }
  }
}
namespace TimberbornTrainer.Core {
  public sealed class TrainerSession {
    public TrainerState State; public ConfigStore Store; public string StartupMessage = "";
  }
}
namespace TimberbornTrainer.Modules {
  public sealed class ResourceModule { public ScienceCommands Science; }
  public sealed class BuildingModule {
    public bool Busy => false; public bool RuntimeError => false; public ConstructionResult LastResult => null;
    public void CompleteAll() { }
  }
  public sealed class BeaverModule { public bool RuntimeError => false; public int HungerCount => 0; public int ThirstCount => 0; public int UnsafeNeeds => 0; }

  public sealed class UiStock : IWarehouseStock {
    public int Amount = 10, Writes, Capacity = 100; public bool Alive = true;
    public bool Usable => Alive;
    public int Read() => Amount; public int UnreservedStock() => Amount; public int FreeCapacity() => Capacity - Amount;
    public void Give(int amount) { Amount += amount; ++Writes; } public void Take(int amount) { Amount -= amount; ++Writes; }
  }
  public sealed class NativeInventoryCatalog : IInventoryCatalog, IMapInventorySource {
    public string SelectedId;
    public readonly Dictionary<string, string> Configured = new Dictionary<string, string> { ["a"] = "ModFood", ["b"] = "Water" };
    public int Releases;
    public void Pick(string id, Timberborn.SingletonSystem.EventBus events) {
      SelectedId = null; events.Post(new Timberborn.SelectionSystem.SelectableObjectUnselectedEvent());
      SelectedId = id; if (id != null) events.Post(new Timberborn.SelectionSystem.SelectableObjectSelectedEvent());
    }
    public void ReleaseSelectedWarehouse() { ++Releases; }
    public InventorySnapshot ReadSelectedWarehouse() {
      ++Reads;
      if (SelectedId == null) return new InventorySnapshot { Message = "请在地图中选择一个仓库。" };
      if (!Configured.TryGetValue(SelectedId, out var good) || Stock(SelectedId)?.Alive != true) return new InventorySnapshot { Message = "不是可编辑仓库或目标失效。" };
      if (good == null) return new InventorySnapshot { Message = "请先通过游戏原生界面配置物品。" };
      var stock = Stock(SelectedId);
      return new InventorySnapshot { Valid = true, Target = Items.First(t => t.Id == SelectedId), Resources = new[] {
        new InventoryResource { GoodId = good, DisplayName = good, Registered = true, Allowed = true, Amount = stock.Amount,
          AvailableStock = stock.Amount, FreeCapacity = stock.FreeCapacity(), TotalCapacity = stock.Capacity }
      } };
    }
    public InventoryScanReport LastScan { get; private set; }
    public int Reads; public readonly UiStock A = new UiStock(), B = new UiStock(), Center = new UiStock();
    public readonly InventoryTarget[] Items = {
      Target("sum", "sum", InventorySource.Settlement), Target("a", "a", InventorySource.Building),
      Target("input", "a", InventorySource.Building), Target("b", "b", InventorySource.Building),
      Target("center", "center", InventorySource.DistrictCenter)
    };
    private static InventoryTarget Target(string id, string owner, InventorySource source) => new InventoryTarget {
      Id = id, OwnerId = owner, OwnerName = "测试地区·建筑" + owner, BuildingType = "原生库存替身",
      DisplayName = id, Source = source, Purpose = id == "input" ? "施工材料库存" : "仓储库存", Abilities = new InventoryAbilities(true, "目录只读")
    };
    public UiStock Stock(string id) => id == "a" ? A : id == "b" ? B : id == "center" ? Center : null;
    public InventoryScanReport Scan() => LastScan = new InventoryScanReport { Completed = true, Sequence = (LastScan?.Sequence ?? 0) + 1 };
    public InventoryDiscovery Discover(InventorySource source) => new InventoryDiscovery {
      Targets = LastScan == null ? Array.Empty<InventoryTarget>() : Items.Where(i => i.Source == source && (Stock(i.Id)?.Alive ?? true)).ToArray(), Message = "测试目录"
    };
    public InventorySnapshot Read(string id) {
      ++Reads; var target = Items.First(i => i.Id == id); var stock = Stock(id);
      if (stock != null && !stock.Alive) return new InventorySnapshot { Message = "目标已失效" };
      return new InventorySnapshot { Target = target, Valid = true, Message = "已读取", Resources =
        (id == "a" ? new[] { "ModFood", "Water" } : new[] { "Water" }).Select(g => new InventoryResource {
          GoodId = g, DisplayName = g, Registered = true, Allowed = true, Amount = stock?.Amount ?? 0,
          AvailableStock = stock?.Amount ?? 0, FreeCapacity = stock?.FreeCapacity() ?? 0,
          CapacityState = CapacityState.Finite, TotalCapacity = 100
        }).ToArray() };
    }
    public bool Focus(string id) => Stock(id)?.Alive ?? false;
    public void Clear() { LastScan = null; }
  }
  public sealed class NativeInventoryEditor : IInventoryEditor, IInventoryEditPreview {
    private readonly NativeInventoryCatalog _catalog; private readonly Func<bool> _enabled;
    public string LastTarget, LastResource;
    public NativeInventoryEditor(NativeInventoryCatalog catalog, Func<bool> enabled) { _catalog = catalog; _enabled = enabled; }
    public string ReadOnlyReason(string target, string resource) => _catalog.SelectedId == target && _catalog.Stock(target)?.Usable == true &&
      _catalog.Configured.TryGetValue(target, out var good) && good == resource ? null : "只读或失效目标";
    public InventoryEditResult Execute(string target, string resource, InventoryWriteOperation operation, string input) {
      LastTarget = target; LastResource = resource;
      return InventoryEditCommands.Execute(() => ReadOnlyReason(target, resource) == null ? _catalog.Stock(target) : null, _enabled, operation, input);
    }
  }
}
