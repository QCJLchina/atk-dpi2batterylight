# U2 Battery Light（U2 电量灯）

适用于 **ATK U2 Ultimate** 鼠标的便携式 Windows 小工具。它周期性读取鼠标 2.4G 无线连接的电量，并把 DPI 指示灯的颜色变成对应的电量颜色，一眼即可看出剩余电量。

## 功能

- 读取 ATK U2 Ultimate 2.4G 模式下的电量信息
- 用 DPI 指示灯颜色显示电量区间：
  - 76–100%：绿色
  - 16–75%：黄色
  - 0–15%：红色
- 阈值和颜色均可在界面中自定义
- 支持 DPI 灯效常亮、呼吸和关闭模式
- 支持常亮亮度、呼吸速度调节，并保存设备灯效配置
- 每 45 秒刷新一次，只在跨越颜色区间或颜色被 ATK HUB 覆盖时才写入，尽量减少对鼠标的写入
- 关闭窗口默认最小化到系统托盘，双击托盘图标可重新打开，从托盘菜单退出
- 退出时默认恢复原来的 DPI 颜色

## 下载使用

仓库内 `U2BatteryLight-v1.0.3/U2BatteryLight.exe` 为已编译好的可执行文件，也可到 [Releases](../../releases) 页面下载。

1. 将 `U2BatteryLight.exe` 放在一个可写的普通文件夹中并运行。
2. 确认鼠标处于 2.4G 模式、8K 接收器已连接。
3. 点击“开始监控”。
4. 第一次启用时，程序会自动记录原始 DPI 颜色。

设置和颜色备份保存在 EXE 同目录的 `U2BatteryLight.ini` 中。

## 迁移到其他电脑

只需复制 `U2BatteryLight.exe`。程序不依赖 Node.js、ATK HUB 或额外 DLL，通常也不需要管理员权限。Windows 10/11 通常已包含所需的 .NET Framework 4.x。

迁移到另一只鼠标时，建议只复制 EXE；程序会为目标鼠标重新记录颜色。

## 从源码构建

源码为单文件 WinForms 程序（`Program.cs`），使用 .NET Framework 自带的 C# 编译器即可构建，无需 Visual Studio：

```bat
csc /target:winexe /platform:anycpu /win32manifest:app.manifest ^
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    /out:U2BatteryLight.exe Program.cs
```

`csc` 可在 "适用于 VS 的开发人员命令提示" 中使用，或直接使用 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。

## 注意事项

- 若同时使用 ATK HUB，ATK HUB 修改 DPI 颜色后，本工具会在下次刷新时重新应用电量颜色。
- 点击“停止监控”或“恢复原色”可恢复备份颜色。
- “重录”会以当前 DPI 颜色替换旧备份；执行前应先停止监控并设置好需要保留的原色。
- 请勿把程序放入 `Program Files` 等受保护目录，否则配置可能无法保存；建议放在桌面工具目录或其他普通文件夹。

## 目录结构

```
├── Program.cs                      # 全部源码（WinForms 单文件）
├── app.manifest                    # 应用清单
└── U2BatteryLight-v1.0.3/
    ├── U2BatteryLight.exe          # 已编译可执行文件（v1.0.3）
    └── README-U2电量灯.md          # 随版本附带的中文说明
```

## License

暂未指定，默认保留所有权利。如需开源许可证请告知。

