using System;
using System.Collections.Generic;
using System.Linq;
using TimberbornTrainer.Modules;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberbornTrainer.UI {
  internal sealed class InventoryPanel : VisualElement {
    private readonly InventoryBrowser _browser;
    private readonly DropdownField _source, _inventory, _good;
    private readonly ScrollView _buildings;
    private readonly Label _status, _target, _stock, _abilities;
    private readonly ActionButton _openList;
    private InventoryTarget[] _ownerInventories = Array.Empty<InventoryTarget>();
    private bool _painting, _disposed;

    internal InventoryPanel(IInventoryCatalog catalog) {
      _browser = new InventoryBrowser(catalog);
      Add(ControlStyles.Text("通用资源浏览（只读架构，待实机验证）"));
      _source = Field("目标类型", new List<string> { "聚居地资源统计", "指定地区中心", "指定建筑库存" }); Add(_source);
      var actions = ControlStyles.Row();
      _openList = new ActionButton("打开建筑列表", Discover); actions.Add(_openList);
      actions.Add(new ActionButton("主动刷新", RefreshSelected)); Add(actions);
      _status = ControlStyles.Text("打开页面后按需读取，不后台扫描库存。"); Add(_status);
      _buildings = new ScrollView(); _buildings.style.maxHeight = 110; Add(_buildings);
      _inventory = Field("具体库存", new List<string> { "请选择目标" }); Add(_inventory);
      _good = Field("资源", new List<string> { "请选择库存" }); Add(_good);
      _target = ControlStyles.Text(""); Add(_target);
      _stock = ControlStyles.Text(""); Add(_stock);
      _abilities = ControlStyles.Text("增加、减少、设置：未开放，等待下一阶段独立审核。"); Add(_abilities);
      _source.RegisterValueChangedCallback(_ => {
        if (_painting || _disposed) return;
        _browser.Switch((InventorySource)_source.index); RenderTargets();
      });
      _inventory.RegisterValueChangedCallback(_ => {
        if (_painting || _disposed) return;
        if (_inventory.index >= 0 && _inventory.index < _ownerInventories.Length) {
          _browser.Select(_ownerInventories[_inventory.index].Id); RenderSnapshot();
        }
      });
      _good.RegisterValueChangedCallback(_ => { if (!_painting && !_disposed) RenderResource(); });
      _openList.SetEnabled(false);
    }
    private static DropdownField Field(string label, List<string> choices) {
      var field = new DropdownField(label, choices, 0);
      field.style.minHeight = 30; field.labelElement.style.color = ControlStyles.Light;
      var input = field.Q(className: "unity-base-popup-field__input");
      if (input != null) { input.style.backgroundColor = new Color(.1f, .2f, .18f); input.style.color = ControlStyles.Light; ControlStyles.Border(input, ControlStyles.Muted); }
      field.style.color = ControlStyles.Light;
      return field;
    }
    internal void Open() {
      if (_disposed || _browser.Opened) return;
      _browser.Switch(InventorySource.Settlement); RenderTargets();
    }
    private void Discover() {
      if (_disposed) return;
      _browser.Discover(); RenderTargets();
    }
    private void RefreshSelected() {
      if (_disposed) return;
      if (_browser.Snapshot?.Target != null) {
        _browser.Select(_browser.Snapshot.Target.Id); RenderSnapshot();
      } else Discover();
    }
    private void RenderTargets() {
      _buildings.Clear(); _ownerInventories = Array.Empty<InventoryTarget>();
      bool settlement = _browser.Source == InventorySource.Settlement;
      _openList.text = _browser.Source == InventorySource.Building ? "打开／刷新建筑列表" : "刷新地区中心列表";
      _openList.Available(!settlement);
      _buildings.style.display = settlement ? DisplayStyle.None : DisplayStyle.Flex;
      foreach (var grouping in _browser.Targets.GroupBy(t => t.OwnerId)) {
        var owner = grouping.First();
        var button = new ActionButton(owner.OwnerName + " · " + owner.BuildingType + " · " + grouping.Count() + " 个库存",
          () => SelectOwner(owner.OwnerId));
        button.style.whiteSpace = WhiteSpace.Normal;
        button.tooltip = "单击选择；双击使用游戏原生定位与高亮。";
        button.RegisterCallback<ClickEvent>(e => {
          if (e.clickCount == 2 && !_disposed) {
            bool focused = _browser.Focus(owner.Id);
            if (!focused) RenderSnapshot();
            _status.text = focused ? "已请求原生镜头定位和高亮。" : _browser.Message;
          }
        });
        _buildings.Add(button);
      }
      if (_browser.Snapshot?.Target != null) SelectOwner(_browser.Snapshot.Target.OwnerId, false);
      else if (settlement && _browser.Targets.Length == 1) SelectOwner(_browser.Targets[0].OwnerId, false);
      else RenderSnapshot();
    }
    private void SelectOwner(string ownerId, bool read = true) {
      _ownerInventories = _browser.Targets.Where(t => t.OwnerId == ownerId).ToArray();
      _painting = true;
      _inventory.choices = _ownerInventories.Select(t => t.DisplayName).ToList();
      if (_inventory.choices.Count == 0) _inventory.choices.Add("没有有效库存");
      int selected = read ? 0 : Array.FindIndex(_ownerInventories, t => t.Id == _browser.Snapshot?.Target?.Id);
      _inventory.SetValueWithoutNotify(_inventory.choices[Math.Max(0, selected)]);
      _painting = false;
      if (read && _ownerInventories.Length > 0) _browser.Select(_ownerInventories[0].Id);
      RenderSnapshot();
    }
    private void RenderSnapshot() {
      _status.text = _browser.Message;
      var snapshot = _browser.Snapshot;
      bool valid = snapshot != null && snapshot.Valid;
      _painting = true;
      if (!valid) {
        if (_browser.Targets.Length == 0) _buildings.Clear();
        _ownerInventories = Array.Empty<InventoryTarget>();
        _inventory.choices = new List<string> { "请选择目标" }; _inventory.SetValueWithoutNotify("请选择目标");
      }
      _inventory.SetEnabled(valid && _ownerInventories.Length > 1);
      _good.choices = valid ? snapshot.Resources.Select(g => g.DisplayName + "（" + g.GoodId + "）").ToList() : new List<string>();
      if (_good.choices.Count == 0) _good.choices.Add("没有可显示的资源");
      _good.SetValueWithoutNotify(_good.choices[0]); _good.SetEnabled(valid && snapshot.Resources.Length > 0);
      _painting = false;
      _target.text = valid ? snapshot.Target.OwnerName + " · " + snapshot.Target.DisplayName + "\n" + snapshot.Target.Purpose : "尚未选择有效库存。";
      _target.tooltip = valid ? snapshot.Target.Id : "";
      var abilities = valid ? snapshot.Target.Abilities : new InventoryAbilities(false, "尚未选择有效目标。");
      _abilities.text = "读取：" + Ability(abilities.Read) + "\n增加：" + Ability(abilities.Add)
        + "\n减少：" + Ability(abilities.Subtract) + "\n设置：" + Ability(abilities.Set);
      RenderResource();
    }
    private static string Ability(InventoryAbility ability) => (ability.Available ? "可用；" : "不可用；") + ability.Reason;
    private static string Number(long? value) => value.HasValue ? value.Value.ToString() : "不适用／未知";
    private void RenderResource() {
      var snapshot = _browser.Snapshot;
      if (snapshot == null || !snapshot.Valid || _good.index < 0 || _good.index >= snapshot.Resources.Length) { _stock.text = ""; return; }
      var row = snapshot.Resources[_good.index];
      bool aggregate = snapshot.Target.Source == InventorySource.Settlement;
      string capacity = row.CapacityState == CapacityState.Aggregate ? "统计汇总，不作为统一存储容量"
        : row.CapacityState == CapacityState.Unlimited ? "原生明确无限"
        : row.CapacityState == CapacityState.Finite ? "有限，总容量 " + Number(row.TotalCapacity)
        : "无限状态未确认；原生总上限 " + Number(row.TotalCapacity);
      _stock.text = (aggregate ? "统计总量：" : "真实数量：") + row.Amount
        + "\n" + (aggregate ? "原生可用统计：" : "未预留可用：") + Number(row.AvailableStock)
        + "；已预留物品：" + Number(row.ReservedStock) + "\n预留空位：" + Number(row.ReservedCapacity)
        + "；可用空位：" + Number(row.FreeCapacity) + "\n容量：" + capacity
        + "；当前资源限额：" + Number(row.ResourceLimit)
        + (aggregate ? "" : "\n注册资源：" + (row.Registered ? "是" : "否") + "；库存声明支持：" + (row.Allowed ? "是" : "否"))
        + "\n" + row.Details;
    }
    internal void Unload() {
      if (_disposed) return;
      _disposed = true; _browser.Dispose(); _ownerInventories = Array.Empty<InventoryTarget>(); _buildings.Clear();
      _inventory.choices.Clear(); _good.choices.Clear();
    }
  }
}
