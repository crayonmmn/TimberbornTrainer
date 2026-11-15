using System;
using System.Linq;
using TimberbornTrainer.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberbornTrainer.Tests {
  public static class EdgeTabTests {
    private static int _checks;
    private static void Check(bool ok,string message){++_checks;if(!ok)throw new Exception(message);}
    private sealed class Fixture : IDisposable {
      internal readonly VisualElement Root=new VisualElement(), Panel=new VisualElement(), Title=new VisualElement();
      internal readonly FloatingWindow View;
      internal readonly Label Tab;
      internal Fixture(bool left) {
        Time.unscaledTime=0;Root.Add(Panel);Panel.Add(Title);
        var text=new Label("标题");Title.Add(text);
        new Timberborn.UILayoutSystem.UILayout().AddAbsoluteItem(Root);
        View=new FloatingWindow(Root,Panel,Title,text);View.SetOpen(true);
        View.State.BeginDrag(270,115);View.State.Drag(left?10:1330,115);View.State.EndDrag(0);View.State.Hover(false,0);
        At(.51f);Frames();Tab=Root.panel.visualTree.Q<Label>("TrainerEdgeTab");
      }
      internal void At(float time){Time.unscaledTime=time;View.Pump();}
      internal void Frames(){for(int i=0;i<65;++i){Time.unscaledTime+=.016f;View.Pump();}}
      internal Vector2 Point=>new Vector2(Tab.style.left+10,Tab.style.top+10);
      internal void Press(){Tab.Dispatch(new MouseEnterEvent());Tab.Dispatch(new MouseDownEvent{mousePosition=Point});}
      public void Dispose(){View.Dispose();}
    }
    public static string Run() {
      _checks=0;
      foreach(bool left in new[]{true,false}) {
        foreach(float wobble in new[]{0f,3f,6f}) using(var f=new Fixture(left)) {
          var p=f.Point;f.Press();
          Check(f.Tab.HasMouseCapture()&&!f.Root.HasMouseCapture()&&!f.View.State.Dragging,"标签自己捕获鼠标，不进入整窗拖动");
          f.Tab.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(p.x+wobble,p.y)});
          f.Tab.Dispatch(new MouseUpEvent{mousePosition=new Vector2(p.x+wobble,p.y)});
          Check(!f.View.State.Hidden&&!f.Tab.HasMouseCapture(),"左右标签单击、微移及恰好6单位均判定点击");
          f.Frames();Check(!f.View.State.Animating,"点击后完成原有展开动画");
        }
        using(var f=new Fixture(left)) {
          float x=f.View.State.X,y=f.View.State.Y;var p=f.Point;
          Check(f.Tab.parent==f.Root.parent,"标签是独立覆盖元素，不受隐藏窗口位置裁切");
          f.Press();f.Tab.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(p.x+6.1f,p.y)});
          f.Tab.Dispatch(new MouseEnterEvent());f.Root.Dispatch(new MouseEnterEvent());f.At(Time.unscaledTime+1);
          Check(f.View.State.Hidden&&f.View.State.X==x&&f.View.State.Y==y,"超过阈值后只移动标签，悬停和调度不唤醒或移动整窗");
          float targetX=left?1884:0;
          f.Root.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(targetX+10,510)});
          Check(Math.Abs(f.Tab.style.left-targetX)<.1f&&f.Tab.style.top==500,"捕获后离开标签区域仍可跨屏拖到另一侧");
          foreach(char c in "wasd") {var key=new KeyDownEvent{character=c};f.Tab.Dispatch(key);Check(!key.Stopped,"标签拖动不拦截WASD");}
          f.Root.Dispatch(new MouseUpEvent{mousePosition=new Vector2(targetX+10,510)});
          f.At(Time.unscaledTime+2);
          Check(f.View.State.Hidden&&!f.Tab.HasMouseCapture()&&f.Tab.style.left==targetX&&f.Tab.style.top==500,"松开仅保留新位置，不产生点击或悬停唤醒");
          Check(f.View.State.X==x&&f.View.State.Y==y,"拖动结束仍未移动隐藏窗口");
          f.Tab.Dispatch(new MouseLeaveEvent());f.Tab.Dispatch(new MouseEnterEvent());float now=Time.unscaledTime;
          f.At(now+.1f);Check(f.View.State.Hidden,"悬停短延迟为按下判定留出时间");
          f.At(now+.25f);f.Frames();Check(!f.View.State.Hidden&&!f.View.State.Animating,"新一次悬停仍可使用原有展开机制");
        }
        using(var f=new Fixture(left)) {
          f.Press();var p=f.Point;f.Tab.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(-500,-500)});
          Check(f.Tab.style.left==0&&f.Tab.style.top==0,"标签不能拖出左上边界");
          f.Tab.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(9999,9999)});
          Check(f.Tab.style.left==1884&&f.Tab.style.top==1010,"标签不能拖出右下边界");
          var screen=f.Root.panel.visualTree;screen.LayoutWidth=480;screen.LayoutHeight=320;
          screen.Dispatch(new GeometryChangedEvent{oldRect=new Rect(0,0,1920,1080),newRect=new Rect(0,0,480,320)});
          Check(f.Tab.style.left==444&&f.Tab.style.top==250,"分辨率缩小时独立标签重新约束在可视区");
          f.Tab.ReleaseMouse();f.Tab.Dispatch(new MouseUpEvent{mousePosition=p});f.At(Time.unscaledTime+1);
          Check(f.View.State.Hidden&&!f.Tab.HasMouseCapture(),"捕获丢失取消手势，后续松开不能误触发点击");
        }
        using(var f=new Fixture(left)) {
          f.Press();var p=f.Point;
          f.Root.Dispatch(new MouseUpEvent{mousePosition=new Vector2(p.x+(left?100:-100),p.y+20)});
          Check(f.View.State.Hidden,"缺少中间移动事件时，松开仍按最终位移判定拖动");
          f.Press();f.Root.Dispatch(new DetachFromPanelEvent());
          Check(!f.Tab.Visible&&!f.Tab.HasMouseCapture()&&!f.View.State.Open,"场景UI脱离时取消标签捕获并隐藏独立标签");
          f.Root.Dispatch(new AttachToPanelEvent());
          Check(f.Root.panel.visualTree.Tree().Count(e=>e.name=="TrainerEdgeTab")==1,"重新挂载不重复创建标签");
        }
        using(var f=new Fixture(left)) {
          var p=f.Point;f.Press();
          f.Tab.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(p.x+(left?7:-7),p.y)});
          f.Tab.Dispatch(new MouseMoveEvent{mousePosition=new Vector2(p.x+1,p.y)});
          f.Tab.Dispatch(new MouseUpEvent{mousePosition=new Vector2(p.x+1,p.y)});
          Check(f.View.State.Hidden,"一旦进入拖动，即使回到按下点附近也不得误判为点击");
        }
      }
      return "边缘标签独立点击/拖动（实际事件适配器＋UI替身，非实机）："+_checks+" 项通过。";
    }
  }
}
