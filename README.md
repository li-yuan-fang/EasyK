# EasyK

**轻松随地大小K**

## ⚠️当前版本为Win7特供版 CEFSharp采用109.1.110版本

**这意味着会有旧版CEFSharp的Bug，以及功能不完整**

**如果是Win7以上版本64位系统请使用➡️**[**⭐主版本⭐**](https://github.com/li-yuan-fang/EasyK)

## 不支持的功能

- **❎登录状态保持**

- **❎打开bilibili视频需要模拟鼠标点击**

## 如何编译

1. 编译[内置音乐播放器](https://github.com/li-yuan-fang/easyk-musicbox/)为静态页面

2. 将编译好的播放器静态页面复制到主程序源代码目录下的```wwwroot/dlna```目录

3. 编译EasyK主程序

4. 复制[**CefSharp.H264**](https://www.nuget.org/packages/CefSharp.H264.x64/109.1.110)到输出目录并替换（可参考[这篇](https://www.cnblogs.com/wintertone/p/18416085)）

5. 编译[前端页面](https://github.com/li-yuan-fang/easyk-frontend/)为静态页面

6. 在输出目录创建子目录**wwwroot**，并将编译好的前端页面复制进去

7. Enjoy
