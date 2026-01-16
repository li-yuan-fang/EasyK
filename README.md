# EasyK

**轻松随地大小K**

## 为什么要做这个

众所周知，大部分的KTV歌都不多，很多小众歌覆盖不到，而且不少都不能像纯K那样简单的投屏，更有甚者声称「能投屏」但是一堆广告而且投出来到电视上永远是竖屏😡😡😡

不过最近发现有不少KTV可以插线投屏（也就是允许你用电脑插HDMI线），为了解决投屏麻烦和点歌次序的问题，随手开发了这个小工具⬇️⬇️⬇️

**EasyK**

## 所以有什么功能

✅能扫码排队点歌

✅支持上传视频

✅支持B站直接播放

✅支持DLNA投屏（纯K那种）

✅投屏不会切歌

✅投屏可以开防插队功能（只有点歌的人能投）

## 要怎么用

参考用户指南➡️

## TODO

- [ ] 适配音乐软件（解决只有音频流时候播放黑屏的问题）

- [ ] ~~修Bug~~
  
  

**⚠️以下为技术内容⚠️**

## 技术特性

- 基于[.Net Framework 4.8](https://go.microsoft.com/fwlink/?linkid=2088631)开发的WinForm程序

- 使用[Kestrel](https://learn.microsoft.com/zh-cn/aspnet/core/fundamentals/servers/kestrel)作为HTTP服务端

- 使用[CEFSharp](https://github.com/cefsharp/CefSharp)作为内置浏览器（实际使用CEFSharp.H264发行版）

- 使用[LibVLCSharp](https://github.com/videolan/libvlcsharp)作为内置播放器

- 点歌界面使用[Vue 3](https://cn.vuejs.org/)和[Vant组件库](https://vant-ui.github.io/)实现

- 理论上支持Windows 7及以上版本系统

## 如何编译

1. 编译EasyK主程序

2. 复制[**CefSharp.H264.x64**](https://www.nuget.org/packages/CefSharp.H264.x64/109.1.110)到输出目录并替换（可参考[这篇](https://www.cnblogs.com/wintertone/p/18416085)）

3. 编译[前端页面](https://github.com/li-yuan-fang/easyk-frontend/)为静态页面

4. 在输出目录创建子目录**wwwroot**，并将编译好的前端页面复制进去

5. Enjoy

## 参考了这些

[Macast](https://github.com/xfangfang/Macast/)

[Universal Plug and Play Device Architecture](https://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v1.1.pdf)

**还有很多零星的参考资料，太多了没办法一一列举见谅**
