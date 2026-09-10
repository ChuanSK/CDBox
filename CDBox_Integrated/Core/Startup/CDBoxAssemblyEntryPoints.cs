using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.Runtime;
using TCPipeAutoDraw.Commands;

// 只让 CAD 扫描固定入口，避免在基础安装中枚举未安装业务的页面类型。
[assembly: ExtensionApplication(typeof(TCPipeCommands))]
[assembly: CommandClass(typeof(TCPipeCommands))]
[assembly: InternalsVisibleTo("CDBox.CoreTests")]
