using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.Stockpiles;
using TimberbornTrainer.Modules;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberbornTrainer.UI {
  internal sealed class InventoryPanel : VisualElement {
    private readonly ResourceModule _resources;
    private readonly DropdownField _warehouse, _good;
    private readonly Label _stock, _result;
    private readonly TextField _amount;
    private readonly ActionButton _set, _add, _subtract;
    private Stockpile[] _sites = Array.Empty<Stockpile>();
    private string[] _goods = Array.Empty<string>();
    internal InventoryPanel(ResourceModule resources) {
      _resources = resources;
      Add(ControlStyles.Text("单仓库库存（待实机验证）"));
      Add(ControlStyles.Text("先在游戏中设置仓库允许的物品；选择坐标对应的已建成仓库。食物用食品仓库，水用水箱。"));
      _warehouse = new DropdownField("仓库", new List<string> { "请刷新仓库列表" }, 0);
      _good = new DropdownField("资源", new List<string> { "请选择仓库" }, 0);
      Paint(_warehouse); Paint(_good);
      Add(_warehouse); Add(_good);
      _warehouse.RegisterValueChangedCallback(_ => SelectWarehouse());
      _good.RegisterValueChangedCallback(_ => RefreshStock());
      Add(new ActionButton("刷新仓库列表", Reload));
      _stock = ControlStyles.Text("库存：未选择"); Add(_stock);
      _amount = ControlStyles.Input("目标或数量", "10"); Add(_amount);
      var row = ControlStyles.Row();
      _set = new ActionButton("设置目标库存", () => Execute(ScienceOperation.Set));
      _add = new ActionButton("增加库存", () => Execute(ScienceOperation.Add));
      _subtract = new ActionButton("减少库存", () => Execute(ScienceOperation.Subtract));
      row.Add(_set); row.Add(_add); row.Add(_subtract); Add(row);
      _result = ControlStyles.Text(""); Add(_result);
    }
    private static void Paint(DropdownField field) {
      field.style.minHeight = 30; field.labelElement.style.color = ControlStyles.Light;
      var input = field.Q(className: "unity-base-popup-field__input");
      if (input != null) { input.style.backgroundColor = new Color(.1f, .2f, .18f); input.style.color = ControlStyles.Light; ControlStyles.Border(input, ControlStyles.Muted); }
      field.style.color = ControlStyles.Light;
    }
    private Stockpile Selected => _warehouse.index >= 0 && _warehouse.index < _sites.Length ? _sites[_warehouse.index] : null;
    private IWarehouseStock Current => Selected && _good.index >= 0 && _good.index < _goods.Length ? _resources.Stock(Selected, _goods[_good.index]) : null;
    private void Reload() {
      try {
        _sites = _resources.Warehouses();
        _warehouse.choices = _sites.Select((s, i) => "仓库" + (i + 1) + "（坐标 " + s.GetComponent<BlockObject>().Coordinates + "）").ToList();
        if (_sites.Length == 0) _warehouse.choices.Add("没有已建成仓库");
        _warehouse.index = 0; SelectWarehouse();
      } catch { _sites = Array.Empty<Stockpile>(); _goods = Array.Empty<string>(); _result.text = "读取仓库失败；不能执行库存修改。"; }
    }
    private void SelectWarehouse() {
      try {
        _goods = Selected ? _resources.Goods(Selected) : Array.Empty<string>();
        _good.choices = _goods.Select(id => _resources.GoodName(id)).ToList();
        if (_goods.Length == 0) _good.choices.Add("没有允许的资源");
        _good.index = 0; RefreshStock();
      } catch { _goods = Array.Empty<string>(); _stock.text = "无法读取资源；请刷新列表。"; }
    }
    internal void RefreshStock() {
      try {
        var stock = Current;
        _stock.text = stock != null && stock.Usable ? "真实库存：" + stock.Read() + "；未预留空位：" + stock.FreeCapacity()
          + "；可减少数量：" + stock.UnreservedStock() : "库存：未选择有效仓库及资源";
      } catch { _stock.text = "库存读取失败；请刷新仓库列表。"; }
    }
    internal void Refresh(bool enabled) { _set.Available(enabled); _add.Available(enabled); _subtract.Available(enabled); RefreshStock(); }
    private void Execute(ScienceOperation operation) {
      var result = _resources.Inventory.Execute(Current, operation, _amount.value);
      _result.text = result.Message + "\n修改前：" + (result.Before?.ToString() ?? "未知") +
        "；实际变化：" + (result.Delta?.ToString("+0;-0;0") ?? "未知") + "；修改后：" + (result.After?.ToString() ?? "未知");
      _result.style.color = result.Success ? ControlStyles.Accent : ControlStyles.Error;
      RefreshStock();
    }
  }
}
