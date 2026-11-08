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
    private readonly Label _status, _target, _stock, _abilities, _scanStatus, _safety;
    private readonly IInventorySafetyDiagnostics _diagnostics;
    private readonly ActionButton _openList, _scan;
    private InventoryTarget[] _ownerInventories = Array.Empty<InventoryTarget>();
    private string[] _resourceIds = Array.Empty<string>();
    private bool _painting, _disposed;

    internal InventoryPanel(IInventoryCatalog catalog) {
      _browser = new InventoryBrowser(catalog);
      _diagnostics = catalog as IInventorySafetyDiagnostics;
      Add(ControlStyles.Text("通用资源浏览（只读架构，待实机验证）"));
      _source = Field("目标类型", new List<string> { "聚居地资源统计", "指定地区中心", "指定建筑库存" }); Add(_source);
      var actions = ControlStyles.Row();
      _scan = new ActionButton("扫描资源与库存", Scan); actions.Add(_scan);
      _openList = new ActionButton("打开建筑列表", Discover); actions.Add(_openList);
      actions.Add(new ActionButton("主动刷新", RefreshSelected)); Add(actions);
      _scanStatus = ControlStyles.Text("尚未扫描；打开列表不会自动扫描场景。"); Add(_scanStatus);
      _status = ControlStyles.Text("打开页面后按需读取，不后台扫描库存。"); Add(_status);
      _status.style.minHeight = 50;
      _buildings = new ScrollView(); _buildings.style.maxHeight = 110; Add(_buildings);
      _inventory = Field("具体库存", new List<string> { "请选择目标" }); Add(_inventory);
      _good = Field("资源", new List<string> { "请选择库存" }); Add(_good);
      _target = ControlStyles.Text(""); Add(_target);
      _stock = ControlStyles.Text(""); Add(_stock);
      _abilities = ControlStyles.Text("增加、减少、设置：未开放，等待下一阶段独立审核。"); Add(_abilities);
      var diagnose = new ActionButton("读取安全诊断（只读）", Diagnose); Add(diagnose);
      diagnose.Available(_diagnostics != null);
      _safety = ControlStyles.Text("安全诊断未读取；仅主动点击时检查选中库存，不授予写入权限。"); Add(_safety);
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
      _good.RegisterValueChangedCallback(_ => {
        if (_painting || _disposed) return;
        if (_good.index >= 0 && _good.index < _resourceIds.Length) _browser.SelectGood(_resourceIds[_good.index]);
        RenderResource();
      });
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
    private void Scan() {
      if (_disposed) return;
      _scan.Available(false);
      try { _browser.Scan(); RenderTargets(); }
      finally { _scan.Available(true); }
    }
    private void RefreshSelected() {
      if (_disposed) return;
      if (_browser.Snapshot?.Target != null) {
        _browser.Select(_browser.Snapshot.Target.Id); RenderTargets();
      } else Discover();
    }
    private void RenderTargets() {
      _buildings.Clear(); _ownerInventories = Array.Empty<InventoryTarget>();
      bool settlement = _browser.Source == InventorySource.Settlement;
      _openList.text = _browser.Source == InventorySource.Building ? "打开建筑列表" : "打开地区中心列表";
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
            if (!focused) RenderTargets();
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
      int selected = Array.FindIndex(_ownerInventories, t => t.Id == _browser.Snapshot?.Target?.Id);
      _inventory.SetValueWithoutNotify(_inventory.choices[Math.Max(0, selected)]);
      _painting = false;
      if (read && _ownerInventories.Length > 0) _browser.Select(_ownerInventories[Math.Max(0, selected)].Id);
      RenderSnapshot();
    }
    private void RenderSnapshot() {
      _status.text = _browser.Message;
      var scan = _browser.LastScan;
      _scan.text = scan == null ? "扫描资源与库存" : "重新扫描";
      _scanStatus.text = scan == null ? "尚未扫描；请主动扫描后再打开列表。"
        : "第 " + scan.Sequence + " 次扫描；耗时 " + scan.ElapsedMilliseconds.ToString("F1") + " 毫秒；" + (scan.Completed ? "完成" : "失败")
          + "\n注册资源 " + scan.RegisteredResources + "；检查实体 " + scan.InspectedEntities + "；原生建筑 " + scan.RecognizedBuildings
          + "\n有可读库存建筑 " + scan.InventoryBuildings + "；可读库存 " + scan.ReadableInventories + "；有效地区中心 " + scan.DistrictCenters
          + "\n排除对象 " + scan.ExcludedObjects + "；排除库存 " + scan.ExcludedInventories
          + (scan.ExclusionReasons.Length == 0 ? "" : "\n" + string.Join("；", scan.ExclusionReasons));
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
      _resourceIds = valid ? snapshot.Resources.Select(g => g.GoodId).ToArray() : Array.Empty<string>();
      if (_good.choices.Count == 0) _good.choices.Add("没有可显示的资源");
      int goodIndex = valid ? Array.FindIndex(snapshot.Resources, r => r.GoodId == _browser.SelectedGoodId) : -1;
      if (valid && goodIndex < 0 && snapshot.Resources.Length > 0) {
        _good.choices.Insert(0, "原资源已失效，请重新选择");
        _resourceIds = new string[] { null }.Concat(_resourceIds).ToArray();
      }
      _good.SetValueWithoutNotify(_good.choices[Math.Max(0, goodIndex)]); _good.SetEnabled(valid && snapshot.Resources.Length > 0);
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
    private void Diagnose() {
      if (_disposed || _diagnostics == null) return;
      var snapshot = _browser.Snapshot;
      if (snapshot == null || !snapshot.Valid || _browser.SelectedGoodId == null) {
        _safety.text = "请先选择有效建筑库存及资源。聚居地汇总不适用。"; return;
      }
      var report = _diagnostics.DiagnoseSafety(snapshot.Target.Id, _browser.SelectedGoodId);
      string identity = report.Identity.Status == InventoryIdentityStatus.Valid ? "本次观察有效"
        : report.Identity.Status == InventoryIdentityStatus.Invalid ? "无效" : "未知";
      _safety.text = "只读诊断 · " + report.ObservedUtc.ToLocalTime().ToString("HH:mm:ss")
        + "\n身份：" + identity + "；用途：" + snapshot.Target.Purpose
        + "\n地区标识：" + report.Identity.District + "；资源：" + report.Identity.Resource
        + "\n物品预留：" + Number(report.ReservedStock) + "；全部容量预留：" + Number(report.ReservedCapacity)
        + "\n资源域 " + report.Rows.Count + " 种；观察自洽：" + (report.ObservationConsistent ? "是" : "否")
        + "；完整性／同步性：未证明\n候选写入资格：关闭；全部写入保持禁用。"
        + "\n" + report.Identity.Reason + "\n" + report.Reason;
      _safety.tooltip = string.Join("\n", report.Rows.Select(r => r.Resource + "：实际 " + r.Amount
        + "，未预留 " + r.Unreserved + "，物品预留 " + r.Reserved + "，容量预留 " + r.CapacityReservation
        + "，未预留空位 " + r.FreeCapacity + "，资源限额 " + r.Limit
        + "，声明允许 " + (r.Declared ? "是" : "否") + "，已注册 " + (r.Registered ? "是" : "否")));
    }
    private void RenderResource() {
      _safety.text = "安全诊断未读取或选择已变化；请主动读取。历史观察不能授权操作。";
      _safety.tooltip = "";
      var snapshot = _browser.Snapshot;
      var row = snapshot?.Resources.FirstOrDefault(r => r.GoodId == _browser.SelectedGoodId);
      if (snapshot == null || !snapshot.Valid || row == null) { _stock.text = ""; return; }
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
      _resourceIds = Array.Empty<string>();
      _inventory.choices.Clear(); _good.choices.Clear();
    }
  }
}
