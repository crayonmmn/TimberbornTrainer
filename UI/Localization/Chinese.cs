namespace TimberbornTrainer.UI.Localization {
  internal static class Chinese {
    internal const string Title = "海狸修改工具";
    internal static readonly string[] Modules = { "资源管理", "建造与生产", "海狸管理", "天气管理", "灾害管理" };
    internal static readonly string[] Descriptions = {
      "木材、食物、水及其他资源；科研点数。",
      "立即完成所有建造：一次点击处理当前有效工地，不持续自动施工。",
      "饥饿与口渴需求分别控制。",
      "天气类型、持续时间与周期状态统一处理。",
      "当前及未来干旱、恶水持续时间；与天气管理共享底层状态。"
    };
    internal const string Placeholder = "此模块尚未实现。当前阶段不会修改游戏参数。";
  }
}
