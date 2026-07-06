# EasyK

**轻松随地大小K**

## 🌟当前版本🌟 ➡️ [✌v1.1.4🐯](https://github.com/li-yuan-fang/EasyK/releases/tag/v1.1.4)

![播放窗口1](./docs/assets/r1.png)

## 为什么要做这个

众所周知，大部分的KTV歌都不多，很多小众歌覆盖不到，而且不少都不能像纯K那样简单的投屏，更有甚者声称**能投屏**但是一堆广告而且投出来到电视上永远是竖屏😡😡😡

不过最近发现有不少KTV可以插线投屏（也就是允许你用电脑插HDMI线），为了解决投屏麻烦和点歌次序的问题，随手开发了这个小工具😋

![播放窗口2](./docs/assets/r2.png)

## 所以有什么功能

✅能扫码排队点歌

✅支持上传视频

✅支持B站直接播放

✅支持DLNA投屏（纯K那种）

✅DLNA音乐模式（DLNA投屏纯音频时自动生成界面）

✅DLNA音乐模式支持双层歌词（日语歌预读加成）

✅实时生成伴奏（音源有人声可以自动消除）

✅平衡随机排序算法（确保随机排序后每个人都有机会轮麦）

✅公平模式（确保每个人都有机会轮麦）

✅投屏不会切歌

✅投屏可以开防插队功能（只有点歌的人能投）

✅在家也可以直接把电脑当点歌机用

## 要怎么用

保存图片发给你的朋友看➡️[**✅使用说明✅**](#%E4%BD%BF%E7%94%A8%E8%AF%B4%E6%98%8E)

小白请看➡️[**❓使用手册❓**](https://github.com/li-yuan-fang/EasyK/blob/master/docs/EasyK%20%E4%BD%BF%E7%94%A8%E6%89%8B%E5%86%8C.pdf)

高手请看Wiki➡️[**⭐用户指南⭐**](https://github.com/li-yuan-fang/EasyK/wiki/EasyK-%E7%94%A8%E6%88%B7%E6%8C%87%E5%8D%97)

![指令界面](./docs/assets/r3.png)

## 使用说明

![使用说明1](./docs/assets/u1.png)

![使用说明2](./docs/assets/u2.png)

![使用说明3](./docs/assets/u3.png)

![使用说明4](./docs/assets/u4.png)

## TODO

- [ ] 优化使用体验

- [ ] 尝试引入新的伴奏处理方案

- [ ] ~~修Bug~~
  
  

**⚠️以下为技术内容⚠️**

## 技术特性

- 基于[.Net Framework 4.8](https://go.microsoft.com/fwlink/?linkid=2088631)开发的WinForm程序

- 使用[Kestrel](https://learn.microsoft.com/zh-cn/aspnet/core/fundamentals/servers/kestrel)作为HTTP服务端

- 使用[CEFSharp](https://github.com/cefsharp/CefSharp)作为内置浏览器（实际使用CEFSharp.H264发行版）

- 使用[LibVLCSharp](https://github.com/videolan/libvlcsharp)作为内置播放器

- 点歌界面使用[Vue 3](https://cn.vuejs.org/)和[Vant组件库](https://vant-ui.github.io/)实现

- 理论上支持Windows 10及以上版本系统

## 如何编译

1. 编译[内置音乐播放器](https://github.com/li-yuan-fang/easyk-musicbox/)为静态页面

2. 将编译好的播放器静态页面复制到主程序源代码目录下的```wwwroot/dlna```目录

3. 编译EasyK主程序

4. 复制支持H264的CefSharp库到输出目录并替换（可参考[编译带H.264的cef(windows)](https://zhuanlan.zhihu.com/p/694014974)）

> 主分支CefSharp版本已升级到126.2，Win7特供版仍然使用109.1.110

5. 编译[前端页面](https://github.com/li-yuan-fang/easyk-frontend/)为静态页面

6. 在输出目录创建子目录**wwwroot**，并将编译好的前端页面复制进去

7. Enjoy

## 参考了这些

[Macast](https://github.com/xfangfang/Macast/)

[Universal Plug and Play Device Architecture](https://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v1.1.pdf)

**还有很多零星的参考资料，太多了没办法一一列举见谅**
