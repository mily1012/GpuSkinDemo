using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 用于动画事件的派发
///
/// 目前项目内有两种模型动画播放方式
/// 1.GUISkinning
/// 2.原生Animator
///
/// 无论使用哪种方式 都是用这个接口派发
/// 事件在lua层进行业务处理
/// 先创建对应的luainst实例
///
/// 如何传递到lua层
/// 1.luaModule设置lua模块
/// 2.在luaInst.Dispatch函数中, 会根据animationEvent.id的配置, 进一步加载lua模块, 然后调用对应的函数, 完成分派
///
/// 参数 functionName对应luaModule string对应事件参数 以|分割
///
/// 事件的编辑通过配套的编辑器进行
///
/// 动画文件可能会有fbx .anim区分 两者需要同时支持 需要放在查找表内
/// TODO 专门的动画预览及事件编辑器
/// </summary>
public abstract class AnimationEventDispatcherInterface : MonoBehaviour
{
    public abstract void DispatchIntervalTime(string clipName, float startTime, float endTime);
}
