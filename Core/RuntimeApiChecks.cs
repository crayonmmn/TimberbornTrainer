using System;
using System.Collections.Generic;
using System.Reflection;

namespace TimberbornTrainer.Core {
  public enum TrainerFeature { Science, Inventory, Construction, Needs }
  // Metadata inspection only. Never invokes native methods or reads game-world objects.
  public static class RuntimeApiChecks {
    public static Func<TrainerFeature, string> Capture(Func<string, Type> resolve = null) {
      resolve = resolve ?? Resolve;
      var errors = new Dictionary<TrainerFeature, string>();
      foreach (TrainerFeature feature in Enum.GetValues(typeof(TrainerFeature))) {
        try { Check(feature, resolve); errors[feature] = null; }
        catch (Exception e) {
          var name = feature == TrainerFeature.Science ? "科研点" : feature == TrainerFeature.Inventory ? "库存编辑" : feature == TrainerFeature.Construction ? "批量建造" : "海狸需求维持";
          errors[feature] = name + "接口兼容性警告（仍可尝试使用）：" + e.Message;
        }
      }
      return feature => errors.TryGetValue(feature, out var error) ? error : "未知功能接口；仅提示，不限制使用。";
    }
    private static Type Resolve(string fullName) {
      var split = fullName.LastIndexOf('.');
      return Type.GetType(fullName + ", " + fullName.Substring(0, split), false);
    }
    private static Type Require(Func<string, Type> resolve, string name) => resolve(name) ?? throw new MissingMemberException(name);
    private static void Method(Func<string, Type> resolve, string owner, string name, Type returns, params Type[] args) {
      var method = Require(resolve, owner).GetMethod(name, BindingFlags.Instance | (name == "GetNeed" ? BindingFlags.NonPublic : BindingFlags.Public), null, args, null);
      if (method == null || method.ReturnType != returns || method.ContainsGenericParameters) throw new MissingMemberException(owner + "." + name);
    }
    private static void Property(Func<string, Type> resolve, string owner, string name, Type returns) {
      var p = Require(resolve, owner).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
      if (p == null || p.PropertyType != returns || p.GetGetMethod() == null || p.GetIndexParameters().Length != 0) throw new MissingMemberException(owner + "." + name);
    }
    private static void Check(TrainerFeature feature, Func<string, Type> resolve) {
      if (feature == TrainerFeature.Science) {
        const string type = "Timberborn.ScienceSystem.ScienceService";
        Property(resolve, type, "SciencePoints", typeof(int));
        Method(resolve, type, "AddPoints", typeof(void), typeof(int));
        Method(resolve, type, "SubtractPoints", typeof(void), typeof(int));
      } else if (feature == TrainerFeature.Inventory) {
        const string type = "Timberborn.InventorySystem.Inventory";
        foreach (var name in new[] { "AmountInStock", "UnreservedCapacity", "UnreservedAmountInStock" }) Method(resolve, type, name, typeof(int), typeof(string));
        var amount = Require(resolve, "Timberborn.Goods.GoodAmount");
        Method(resolve, type, "GiveExisting", typeof(void), amount);
        Method(resolve, type, "TakeExisting", typeof(void), amount);
        Property(resolve, "Timberborn.TimeSystem.SpeedManager", "CurrentSpeed", typeof(float));
      } else if (feature == TrainerFeature.Construction) {
        Method(resolve, "Timberborn.ConstructionSites.ConstructionSite", "FinishNow", typeof(void));
        Property(resolve, "Timberborn.BlockSystem.BlockObject", "IsFinished", typeof(bool));
        Property(resolve, "Timberborn.BlockSystem.BlockObject", "IsUnfinished", typeof(bool));
      } else {
        const string manager = "Timberborn.NeedSystem.NeedManager";
        var need = Require(resolve, "Timberborn.NeedSystem.Need");
        Method(resolve, manager, "GetNeed", need, typeof(string));
        Method(resolve, manager, "ResetNeed", typeof(void), typeof(string));
        Method(resolve, manager, "GetNeedPoints", typeof(float), typeof(string));
        Method(resolve, manager, "HasNeed", typeof(bool), typeof(string));
        Method(resolve, manager, "GetNeedSpec", Require(resolve, "Timberborn.NeedSpecs.NeedSpec"), typeof(string));
        Property(resolve, "Timberborn.NeedSystem.Need", "Enabled", typeof(bool));
        foreach (var name in new[] { "MaximumValue", "StartingValue" }) Property(resolve, "Timberborn.NeedSpecs.NeedSpec", name, typeof(float));
        int overloads = 0;
        foreach (var m in Require(resolve, manager).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)) if (m.Name == "GetNeed") ++overloads;
        if (overloads != 1) throw new MissingMemberException(manager + ".GetNeed存在歧义");
        var updating = need.GetField("_updateEnabled", BindingFlags.NonPublic | BindingFlags.Instance);
        if (updating == null || updating.FieldType != typeof(bool) || updating.IsInitOnly) throw new MissingMemberException("Need._updateEnabled");
      }
    }
  }
}
