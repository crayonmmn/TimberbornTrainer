using System;
using System.Linq;
using TimberbornTrainer.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberbornTrainer.Tests {
  public static class FloatingWindowTests {
    private static int _checks;
    private static void Check(bool ok, string message) { ++_checks; if (!ok) throw new Exception(message); }
    private static FloatingWindowState New() { var s=new FloatingWindowState();s.Resize(1920,1080,600,650,0);s.SetOpen(true,0);return s; }
    private static void Dock(FloatingWindowState s, bool left, float now=0) {
      s.BeginDrag(s.X+10,s.Y+10);s.Drag(left?10:s.ViewWidth-s.Width+10,115);s.EndDrag(now);s.Hover(false,now);
    }
    private static void Frames(FloatingWindowState s,float start,int count=60) { for(int i=1;i<=count;++i)s.Advance(start+i*.016f,.016f); }
    public static string Run() {
      _checks=0;
      foreach(bool left in new[]{true,false}) {
        var s=New();Dock(s,left);
        Check(s.Edge==(left?DockEdge.Left:DockEdge.Right)&&!s.Hidden,"左右边缘对称吸附并保持初始展开");
        s.Advance(.49f,.016f);Check(!s.Hidden,"吸附后0.5秒内不隐藏");
        float expanded=s.X;s.Advance(.51f,.016f);
        Check(s.Hidden&&s.Animating&&s.X!=expanded,"延迟后滑动，不突然消失");
        Check(Math.Abs(s.X-expanded)<s.Width-FloatingWindowState.TabWidth,"首帧不是跳到隐藏终点");
        Frames(s,.51f);float hiddenX=left?-564:1884;
        Check(Math.Abs(s.X-hiddenX)<.11f&&!s.Animating&&!s.NeedsUpdate,"隐藏后保留36单位标签且停止动画调度");
        s.Hover(true,2);Check(!s.Hidden&&s.Animating,"鼠标进入唤醒时反向平滑展开");Frames(s,2);
        Check(Math.Abs(s.X-expanded)<.11f,"展开回到对应屏幕边缘");
        s.Advance(20,.05f);Check(!s.Hidden,"鼠标仍在窗口时不隐藏");
        s.Hover(false,20);s.Interaction(true,20);s.Advance(21,.05f);Check(!s.Hidden,"控件鼠标交互未结束时不隐藏");
        s.Interaction(false,21);s.Edit(true,21);s.Advance(22,.05f);Check(!s.Hidden,"文本或选择器编辑期间不隐藏");
        s.Edit(false,22);s.Advance(22.49f,.016f);Check(!s.Hidden,"交互结束重新计算延迟");
        s.Advance(22.51f,.016f);Frames(s,22.51f);
        float grab = left ? s.Width-18 : 18;
        s.BeginDrag(s.X+grab,s.Y+10);s.Drag(400+grab,210);
        Check(s.Dragging&&!s.Hidden&&s.Edge==DockEdge.None,"从隐藏边缘拖出解除隐藏及停靠");
        s.EndDrag(24);s.Hover(false,24);s.Advance(100,.05f);
        Check(s.Edge==DockEdge.None&&!s.Hidden&&s.X==400,"拖到中央后长时间保持展开");
        Dock(s,left,101);s.Advance(101.51f,.016f);Check(s.Hidden,"重新停靠后重新启用自动隐藏");
        s.SetOpen(false,102);s.Advance(200,.05f);Check(!s.Hidden&&!s.Open,"手动关闭不留隐藏动画");
        var edge=s.Edge;s.SetOpen(true,200);Check(s.Edge==edge&&!s.Hidden,"重新打开保留停靠位置，不重建状态");
      }
      {
        var s=New();s.BeginDrag(270,115);s.Drag(-1000,-1000);s.EndDrag(0);
        Check(s.X==0&&s.Y==0,"拖动不能丢失在左上屏幕外");
        s.BeginDrag(10,10);s.Drag(9000,9000);s.EndDrag(0);
        Check(s.X==1320&&s.Y==430,"拖动不能丢失在右下屏幕外");
        s.Resize(480,320,600,650,1);Check(s.Width==480&&s.Height==320&&s.X==0&&s.Y==0,"缩小分辨率后位置和尺寸约束有效");
        s.Resize(float.NaN,0,1,1,2);Check(s.Width==480,"忽略无效布局，不产生NaN位置");
      }
      {
        var root=new VisualElement();var panel=new VisualElement();var title=new VisualElement();var text=new Label("标题");
        int clicks=0;var close=new ActionButton("×",()=>++clicks);var button=new ActionButton("操作",()=>++clicks);
        var field=new TextField("输入");var slider=new Slider();var dropdown=new DropdownField("目标",new System.Collections.Generic.List<string>{"甲","乙"},0);
        root.Add(panel);panel.Add(title);title.Add(text);title.Add(close);panel.Add(button);panel.Add(field);panel.Add(slider);panel.Add(dropdown);
        new Timberborn.UILayoutSystem.UILayout().AddAbsoluteItem(root);Time.unscaledTime=0;
        var view=new FloatingWindow(root,panel,title,text);view.SetOpen(true);
        Check(!button.focusable&&!title.focusable&&field.focusable,"普通控件不取得键盘焦点，文本框保持可编辑");
        root.Dispatch(new MouseEnterEvent());button.Dispatch(new MouseLeaveEvent());
        Check(view.State.Hovered,"子控件离开事件不能误判为鼠标离开整个窗口");
        button.Dispatch(new DetachFromPanelEvent());Check(view.State.Open,"子控件重建不会关闭整个窗口状态机");
        panel.Dispatch(new MouseDownEvent());root.Dispatch(new MouseLeaveEvent());
        Check(!view.State.Interacting,"未捕获鼠标的空白区域离开后不残留交互锁定");
        slider.Dispatch(new MouseDownEvent());slider.CaptureMouse();root.Dispatch(new MouseLeaveEvent());
        Check(view.State.Interacting,"滑块捕获期间离开窗口仍保持交互保护");slider.ReleaseMouse();
        Check(!view.State.Interacting,"滑块释放捕获后恢复自动隐藏资格");
        button.Dispatch(new MouseDownEvent{mousePosition=new Vector2(280,180)});
        Check(!view.State.Dragging,"内部按钮不能启动拖窗");button.Click();root.Dispatch(new MouseUpEvent());
        Check(clicks==1,"普通按钮原有回调正常执行一次");
        close.Dispatch(new MouseDownEvent());Check(!view.State.Dragging,"标题栏关闭按钮也不启动拖窗");root.Dispatch(new MouseUpEvent());
        text.Dispatch(new MouseDownEvent{mousePosition=new Vector2(270,115)});
        Check(view.State.Dragging&&root.HasMouseCapture(),"标题文本按下捕获鼠标");
        button.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(410,215)});
        Check(view.State.X==400&&view.State.Y==205,"捕获后鼠标经过内部按钮只移动窗口");
        var key=new KeyDownEvent{character='w'};title.Dispatch(key);Check(!key.Stopped&&view.State.Dragging,"拖动不消费键盘W事件");
        root.Dispatch(new MouseUpEvent{mousePosition=new Vector2(410,215)});
        Check(!view.State.Dragging&&!root.HasMouseCapture(),"释放鼠标结束拖动和捕获");
        foreach(var control in new VisualElement[]{root,title,button,slider,dropdown}) {
          foreach(char c in "wasd") {var e=new KeyDownEvent{character=c};control.Dispatch(e);Check(!e.Stopped,"局部窗口处理器不截获普通控件上的WASD");}
        }
        field.Focus();Check(view.State.Editing,"文本焦点阻止自动隐藏");
        button.Dispatch(new MouseDownEvent());Check(!view.State.Editing&&root.panel.focusController.focusedElement==null,"点击普通控件释放旧文本焦点，无需另点地图");root.Dispatch(new MouseUpEvent());
        dropdown.Focus();Check(view.State.Editing,"选择器编辑防止收起");dropdown.value="乙";
        Check(!view.State.Editing,"选择完成释放交互保护");
        root.Dispatch(new WheelEvent());Check(view.State.Interacting,"滚动暂时保护窗口");
        Time.unscaledTime=.3f;view.Pump();Check(!view.State.Interacting,"滚动保护结束后不遗留永久锁定");
        root.Dispatch(new GeometryChangedEvent{oldRect=new Rect(0,0,600,650),newRect=new Rect(400,205,600,650)});
        Check(view.State.X==400,"只改变位置的布局事件不干扰拖动和动画");
        field.value="保留输入";
        text.Dispatch(new MouseDownEvent{mousePosition=new Vector2(410,215)});
        root.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(10,115)});
        root.Dispatch(new MouseUpEvent{mousePosition=new Vector2(10,115)});
        Check(view.State.Edge==DockEdge.Left&&!view.State.Hidden,"实际标题事件完成左停靠，鼠标仍在内时保持展开");
        root.Dispatch(new MouseLeaveEvent());Time.unscaledTime=1;view.Pump();
        for(int i=0;i<60;++i){Time.unscaledTime+=.016f;view.Pump();}
        var tab=root.panel.visualTree.Q<Label>("TrainerEdgeTab");
        Check(view.State.Hidden&&tab.Visible,"实际调度回调将窗口滑出并保留标签");
        var hiddenKey=new KeyDownEvent{character='a'};tab.Dispatch(hiddenKey);Check(!hiddenKey.Stopped,"隐藏标签不拦截A键");
        tab.Dispatch(new MouseEnterEvent());for(int i=0;i<60;++i){Time.unscaledTime+=.016f;view.Pump();}
        Check(!view.State.Hidden&&!view.State.Animating&&view.State.X==0,"标签事件自动完整唤醒");
        root.Dispatch(new MouseEnterEvent());tab.Dispatch(new MouseLeaveEvent());
        Check(field.value=="保留输入"&&clicks==1&&root.schedule.Items.Count==1,"隐藏展开不重建控件、不重放命令、不累积调度器");
        var revealedKey=new KeyDownEvent{character='d'};button.Dispatch(revealedKey);Check(!revealedKey.Stopped,"唤醒后普通按钮不拦截D键");
        text.Dispatch(new MouseDownEvent{mousePosition=new Vector2(10,115)});
        root.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(460,215)});
        root.Dispatch(new MouseUpEvent{mousePosition=new Vector2(460,215)});
        root.Dispatch(new MouseLeaveEvent());Time.unscaledTime+=20;view.Pump();
        Check(view.State.Edge==DockEdge.None&&!view.State.Hidden&&view.State.X==450,"实际拖出后长期保持中央展开");
        var screen=root.panel.visualTree;screen.LayoutWidth=480;screen.LayoutHeight=320;
        screen.Dispatch(new GeometryChangedEvent{oldRect=new Rect(0,0,1920,1080),newRect=new Rect(0,0,480,320)});
        Check(view.State.Width==480&&view.State.X==0&&view.State.Y==0,"原生形状布局事件更新可视边界");
        root.Dispatch(new DetachFromPanelEvent());Check(!view.State.Open,"脱离UI面板停止窗口状态机");
        root.Dispatch(new AttachToPanelEvent());Check(view.State.Open,"重新挂载与实际面板可见状态同步");
        view.Dispose();Check(!screen.Tree().Any(e=>e.name=="TrainerEdgeTab")&&root.schedule.Items.All(i=>i.Paused),"卸载删除边缘标签并停止所有本窗口调度");
        text.Dispatch(new MouseDownEvent());Check(!root.HasMouseCapture(),"卸载后无残留拖动事件");
      }
      return "悬浮窗口状态机/实际事件适配器（UI替身，不是游戏视角实测）："+_checks+" 项通过。";
    }
  }
}
