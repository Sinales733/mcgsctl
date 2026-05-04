using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

internal static partial class Program
{
    private const uint SaveCommandId = 57603;
    private const uint CheckCommandId = 32786;
    private const uint NativeStaticTextCommandId = 32907;
    private const uint NativeLampCommandId = 32941;
    private const string AnimationEditButtonText = "\u52a8\u753b\u7ec4\u6001";
    private const string NativeStaticTextDialogTitle = "\u6807\u7b7e\u52a8\u753b\u7ec4\u6001\u5c5e\u6027\u8bbe\u7f6e";
    private const string NativeLampDialogTitle = "\u52a8\u753b\u663e\u793a\u6784\u4ef6\u5c5e\u6027\u8bbe\u7f6e";
    private const string ProcessName = "McgsSetE";
    private const string ToolVersion = "0.4.0";

    [STAThread]
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            if (args.Length > 0 && (args[0].Equals("--version", StringComparison.OrdinalIgnoreCase) ||
                                    args[0].Equals("version", StringComparison.OrdinalIgnoreCase)))
            {
                PrintVersion();
                return 0;
            }

            if (args.Length == 0 || Has(args, "--help") || Has(args, "-h"))
            {
                PrintUsage();
                return 0;
            }

            return args[0].ToLowerInvariant() switch
            {
                "doctor" => Doctor(args),
                "open" => Open(args),
                "tree" => Tree(args),
                "children" => Children(args),
                "find" => Find(args),
                "windows" => Windows(args),
                "menus" => Menus(args),
                "command" => Command(args),
                "mdi" => Mdi(args),
                "guiinfo" => GuiInfo(args),
                "modules" => Modules(args),
                "wndproc" => WndProc(args),
                "pe" => Pe(args),
                "strings" => Strings(args),
                "sendmsg" => SendMsg(args),
                "notify" => Notify(args),
                "focus" => Focus(args),
                "toolbar" => Toolbar(args),
                "treeview" => TreeView(args),
                "listview" => ListView(args),
                "combo" => Combo(args),
                "list" => List(args),
                "tab" => Tab(args),
                "check" => CheckControl(args),
                "click" => Click(args),
                "point" => Point(args),
                "drag" => Drag(args),
                "set-text" => SetText(args),
                "get-text" => GetText(args),
                "keys" => Keys(args),
                "wait" => Wait(args),
                "popup" => Popup(args),
                "capture" => Capture(args),
                "run" => RunScript(args),
                "close" => Close(args),
                "snapshot" => Snapshot(args),
                "mce" => Mce(args),
                "verify" => Verify(args),
                "canvas" => Canvas(args),
                "mcgs" => Mcgs(args),
                "layout" => Layout(args),
                "workflow" => Workflow(args),
                "candidate" => Candidate(args),
                "profile" => Profile(args),
                "safety" => Safety(args),
                _ => Fail($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            if (Has(args, "--verbose")) Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
mcgsctl - MCGS embedded editor automation helper

Commands:
  mcgsctl --version
  mcgsctl doctor [--project <mce>] [--editor <exe>]
  mcgsctl open --project <mce> [--editor <exe>]
  mcgsctl tree [--pid <pid>]
  mcgsctl children [--pid <pid>] [--hwnd|--root <hex>] [--class <class>] [--text <text>]
  mcgsctl find [--pid <pid>] [--hwnd|--root <hex>] [--class <class>] [--text <text>] [--index <n>]
  mcgsctl windows [--pid <pid>]
  mcgsctl menus [--pid <pid>]
  mcgsctl command --id <menu-id> [--pid <pid>] [--hwnd <hex>] [--target main|mdi|view] [--send] [--hiword <n>]
  mcgsctl mdi [--pid <pid>] [--hwnd <main>]
  mcgsctl guiinfo [--pid <pid>] [--hwnd <hex>]
  mcgsctl modules [--pid <pid>] [--filter <text>]
  mcgsctl wndproc --hwnd <hex>
  mcgsctl pe exports --file <dll-or-exe> [--filter <text>]
  mcgsctl strings --file <file> [--filter <text>] [--encoding ansi|unicode|both] [--min <n>] [--limit <n>]
  mcgsctl sendmsg --hwnd <hex> --msg <n> [--wparam <n>] [--lparam <n>] [--post]
  mcgsctl notify --hwnd <control> --code <n> [--parent <hwnd>] [--send] [--delay <ms>]
  mcgsctl focus --hwnd <hex>
  mcgsctl toolbar --hwnd <hex> [--buttons] [--click-index <n>|--click-id <id>] [--command-index <n>|--command-id <id>]
  mcgsctl treeview --hwnd <hex> [--items] [--caret] [--select-root] [--expand-root] [--select <item>|--select-text <text>] [--expand <item>|--expand-text <text>] [--notify-selchanged <item>|--notify-selchanged-text <text>] [--unicode] [--send] [--rect <item>] [--text <item>]
  mcgsctl listview --hwnd <hex> [--items] [--select-index <n>|--select-text <text>] [--double-index <n>|--double-text <text>] [--mouse]
  mcgsctl combo --hwnd <hex> [--items] [--select-index <n>] [--select-text <text>]
  mcgsctl list --hwnd <hex> [--items] [--select-index <n>] [--select-text <text>]
  mcgsctl tab --hwnd <hex> [--items] [--select-index <n>|--select-text <text>] [--mouse]
  mcgsctl check --hwnd <hex> (--on|--off|--toggle)
  mcgsctl click --text <button-text> [--pid <pid>] [--hwnd|--root <hex>] [--mouse]
  mcgsctl point --hwnd <hex> --x <n> --y <n> [--right] [--double] [--mouse]
  mcgsctl drag --hwnd <hex> --x1 <n> --y1 <n> --x2 <n> --y2 <n> [--mouse]
  mcgsctl set-text --hwnd <hex> (--text <text>|--file <txt>) [--paste]
  mcgsctl get-text --hwnd <hex>
  mcgsctl keys --text <sendkeys>
  mcgsctl wait [--pid <pid>] [--title <text>] [--class <class>] [--timeout <sec>]
  mcgsctl popup [--pid <pid>] [--open-hwnd <hex> --x <n> --y <n>] [--context] [--choose-index <n>|--choose-id <id>|--choose-text <text>] [--exact] [--mouse]
  mcgsctl capture [--pid <pid>] [--out <dir>]
  mcgsctl run --file <actions.json> [--pid <pid>]
  mcgsctl snapshot [--project <mce>] [--pid <pid>] [--out <dir>]
  mcgsctl mce export --project <mce> [--out <dir>]
  mcgsctl verify --project <mce> --spec <json>
  mcgsctl mcgs inventory --project <candidate.mce> --out <dir> [--toolbar-probe <toolbar-probe.json>]
  mcgsctl mcgs tool-catalog --project <candidate.mce> --out <dir> [--toolbar-probe <toolbar-probe.json>]
  mcgsctl mcgs tool-probe --project <candidate.mce> --tool-id <id> --out <dir> [--tool-catalog <tool-catalog.json>]
  mcgsctl mcgs tool-sweep --project <candidate.mce> --out <dir> [--tool-catalog <tool-catalog.json>]
  mcgsctl canvas inspect --project <candidate.mce> --out <dir> [--window-index <n>]
  mcgsctl canvas context-menu-probe --project <candidate.mce> --out <dir> [--window-index <n>] [--x <n> --y <n>]
  mcgsctl canvas clipboard-probe --project <candidate.mce> --out <dir> [--window-index <n>] [--select-all]
  mcgsctl canvas toolbar-probe --project <candidate.mce> --out <dir> [--window-index <n>]
  mcgsctl canvas mce-geometry-probe --project <candidate.mce> --out <dir>
  mcgsctl canvas property-map-probe --project <candidate.mce> --row-key <key> --out <dir> [--semantic-map <semantic-map.json>]
  mcgsctl layout validate --layout <layout.json> [--safety <safety-spec.json>] [--canvas-objects <canvas-objects.json>] [--placement explicit|internal-occupancy] [--out <file-or-dir>]
  mcgsctl layout preview --layout <layout.json> --out <dir> [--safety <safety-spec.json>] [--canvas-objects <canvas-objects.json>] [--placement explicit|internal-occupancy]
  mcgsctl layout readback --project <candidate.mce> --layout <layout.json> --out <dir> [--canvas-objects <canvas-objects.json>] [--placement explicit|internal-occupancy]
  mcgsctl candidate summarize --workdir <runDir>
  mcgsctl candidate validate --workdir <runDir> [--approval <approval.json>]
  mcgsctl profile check (--project <candidate.mce>|--workdir <runDir>) --profile <profile.json> [--facts-only] [--allow-profile-drift]
  mcgsctl safety scan-awl --file <plc.awl> --spec <safety-spec.json>
  mcgsctl workflow run project.check (--source <mce>|--project <copy.mce>|--pid <pid>) [--workdir <dir>] [--out <dir>] [--allow-attached]
  mcgsctl workflow run project.check-save (--source <mce>|--project <copy.mce>) [--workdir <dir>] [--out <dir>]
  mcgsctl workflow run project.apply-candidate --source <official.mce> --candidate <candidate.mce> --approval <approval.json>
  mcgsctl workflow run project.rollback --rollback <rollbackDir> --target <official.mce>
  mcgsctl workflow run safety.verify --project <candidate.mce> --spec <safety-spec.json> --evidence-dir <runDir> [--awl <plc.awl>]
  mcgsctl workflow run window.layout.apply (--source <mce>|--project <copy.mce>) --layout <layout.json> [--workdir <dir>] [--safety <safety-spec.json>] [--canvas-objects <canvas-objects.json>] [--placement explicit|internal-occupancy]
  mcgsctl workflow run realtime-db.add (--source <mce>|--project <copy.mce>) --name <object> [--type switch|numeric|string|event|group] [--initial <value>] [--unit <text>] [--note <text>]
  mcgsctl workflow run window.static-text.add (--source <mce>|--project <copy.mce>) --text <label> [--window-index <n>] [--x <n> --y <n> --width <n> --height <n>]
  mcgsctl workflow run window.lamp.add-native (--source <mce>|--project <copy.mce>) --text <label> --expression <expr> [--window-index <n>] [--x <n> --y <n> --width <n> --height <n>]
  mcgsctl workflow run window.button.add-momentary (--source <mce>|--project <copy.mce>) --text <label> --variable <name> [--window-index <n>] [--x <n> --y <n> --width <n> --height <n>]
  mcgsctl workflow run device.channel.map (--source <mce>|--project <copy.mce>) --area V --address 603 --count 4 [--data-type-index <n>] [--connect-base <name>] [--expected-channel <text>]
  mcgsctl workflow run script.edit (--source <mce>|--project <copy.mce>) (--text <script>|--file <txt>) [--event down|up] [--button-text <label>] [--verify-token <text>] [--allow-create-dataobjects]
  mcgsctl workflow run window.indicator.add (--source <mce>|--project <copy.mce>) --text <label> --expression <expr> [--window-index <n>] [--x <n> --y <n> --width <n> --height <n>]
  mcgsctl close --save|--discard [--pid <pid>]
""");
    }

    private static void PrintVersion()
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        WriteJson(new
        {
            name = "mcgsctl",
            version = ToolVersion,
            commit = Environment.GetEnvironmentVariable("MCGSCTL_COMMIT") ?? TryGitCommit(),
            runtime = RuntimeInformation.FrameworkDescription,
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            assemblyPath,
            buildTimeUtc = File.Exists(assemblyPath) ? File.GetLastWriteTimeUtc(assemblyPath).ToString("O") : null
        });
    }

    private static int Doctor(string[] args)
    {
        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        var project = FullPath(Opt(args, "--project") ?? DefaultProject());
        var toolRoot = ToolPaths.FindToolRoot();
        var failures = 0;
        var warnings = 0;

        Check("MCGS editor", File.Exists(editor), editor, ref failures);
        Check("default project", File.Exists(project), project, ref failures);
        Check("visible desktop", Environment.UserInteractive, "GUI automation requires an interactive user session", ref failures);

        var inspect = ToolPaths.DefaultInspectPath();
        Check("Windows SDK Inspect", File.Exists(inspect), inspect, ref failures);

        var java = ProcessRunner.TryRun("java", "-version", Environment.CurrentDirectory, 5000);
        Check("Java runtime", java.ExitCode == 0, FirstLine(java.StdErr + java.StdOut), ref failures);
        var javac = ProcessRunner.TryRun("javac", "-version", Environment.CurrentDirectory, 5000);
        Check("Java compiler", javac.ExitCode == 0, FirstLine(javac.StdErr + javac.StdOut), ref failures);

        var jars = ToolPaths.JackcessJars(toolRoot).ToArray();
        Check("Jackcess jars", jars.All(File.Exists), string.Join("; ", jars.Select(Path.GetFileName)), ref failures);

        var csproj = Path.Combine(toolRoot, "McgsCtl.csproj");
        Warn("UI automation driver", File.Exists(csproj), "Win32/MFC handle primitives; FlaUI is not required", ref warnings);

        if (File.Exists(project))
        {
            var looksJet = LooksLikeJetDatabase(project);
            Check(".MCE Access/Jet signature", looksJet, "expected Standard Jet DB header", ref failures);
            if (looksJet && java.ExitCode == 0 && javac.ExitCode == 0 && jars.All(File.Exists))
            {
                try
                {
                    MceExporter.EnsureCompiled(toolRoot);
                    Check("MCE exporter compile", true, "Java helper compiled", ref failures);
                }
                catch (Exception ex)
                {
                    Check("MCE exporter compile", false, ex.Message, ref failures);
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"doctor complete: {failures} failure(s), {warnings} warning(s)");
        return failures == 0 ? 0 : 1;
    }

    private static int Open(string[] args)
    {
        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        var project = RequiredPath(args, "--project");
        if (!File.Exists(editor)) return Fail("MCGS editor not found: " + editor);
        if (!File.Exists(project)) return Fail("Project not found: " + project);

        var psi = new ProcessStartInfo(editor, Quote(project))
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
        };
        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start MCGS editor.");
        var hwnd = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
        var info = WindowInfo.FromHandle(hwnd);
        WriteJson(new
        {
            processId = process.Id,
            mainWindowHandle = "0x" + hwnd.ToInt64().ToString("X"),
            title = info.Text,
            className = info.ClassName
        });
        return 0;
    }

    private static int Tree(string[] args)
    {
        var hwnd = ResolveWindow(args);
        foreach (var line in UiAutomation.WindowTreeLines(hwnd))
        {
            Console.WriteLine(line);
        }
        return 0;
    }

    private static int Children(string[] args)
    {
        var hwnd = ResolveWindow(args);
        var className = Opt(args, "--class");
        var text = Opt(args, "--text");
        var children = UiAutomation.EnumerateChildren(hwnd)
            .Select(WindowInfo.FromHandle)
            .Where(w => string.IsNullOrWhiteSpace(className) || w.ClassName.Contains(className, StringComparison.OrdinalIgnoreCase))
            .Where(w => string.IsNullOrWhiteSpace(text) || w.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        WriteJson(children);
        return 0;
    }

    private static int Find(string[] args)
    {
        var hwnd = ResolveWindow(args);
        var className = Opt(args, "--class");
        var text = Opt(args, "--text");
        var matches = UiAutomation.EnumerateChildren(hwnd)
            .Select(WindowInfo.FromHandle)
            .Where(w => string.IsNullOrWhiteSpace(className) || w.ClassName.Contains(className, StringComparison.OrdinalIgnoreCase))
            .Where(w => string.IsNullOrWhiteSpace(text) || w.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (OptInt(args, "--index") is int index)
        {
            WriteJson(index >= 0 && index < matches.Length ? matches[index] : null!);
        }
        else
        {
            WriteJson(matches);
        }
        return 0;
    }

    private static int Windows(string[] args)
    {
        var pid = OptInt(args, "--pid");
        var windows = pid.HasValue
            ? UiAutomation.TopWindowsForPid(pid.Value).Select(WindowInfo.FromHandle).ToArray()
            : UiAutomation.AllMcgsWindows().Select(WindowInfo.FromHandle).ToArray();
        WriteJson(windows);
        return 0;
    }

    private static int Menus(string[] args)
    {
        var hwnd = ResolveMainWindow(args);
        var entries = UiAutomation.GetMenus(hwnd);
        foreach (var entry in entries)
        {
            Console.WriteLine(entry.Id.HasValue
                ? $"{entry.Id.Value}\t{entry.Path}"
                : $"-\t{entry.Path}");
        }
        return 0;
    }

    private static int Command(string[] args)
    {
        var id = (uint)ParseInt(args, "--id", -1);
        if (id == uint.MaxValue) return Fail("Missing or invalid --id.");
        var hwnd = ResolveCommandWindow(args);
        var hiword = ParseInt(args, "--hiword", 0);
        UiAutomation.SendCommand(hwnd, id, Has(args, "--send"), hiword);
        Console.WriteLine($"sent command {id} to 0x{hwnd.ToInt64():X}");
        return 0;
    }

    private static int Mdi(string[] args)
    {
        var hwnd = ResolveMainWindow(args);
        WriteJson(UiAutomation.GetMdiInfo(hwnd));
        return 0;
    }

    private static int GuiInfo(string[] args)
    {
        var hwnd = Has(args, "--hwnd") ? ResolveWindow(args) : ResolveMainWindow(args);
        if (Has(args, "--activate")) UiAutomation.ActivateForInput(hwnd);
        WriteJson(UiAutomation.GetGuiInfo(hwnd));
        return 0;
    }

    private static int Modules(string[] args)
    {
        var pid = OptInt(args, "--pid");
        if (!pid.HasValue)
        {
            var hwnd = ResolveMainWindow(args);
            pid = UiAutomation.GetWindowProcessId(hwnd);
        }

        var modules = ProcessDiagnostics.GetModules(pid.Value);
        if (Opt(args, "--filter") is string filter)
        {
            modules = modules
                .Where(m => m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            m.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        WriteJson(modules);
        return 0;
    }

    private static int WndProc(string[] args)
    {
        var hwnd = ResolveWindow(args);
        var pid = UiAutomation.GetWindowProcessId(hwnd);
        var modules = ProcessDiagnostics.GetModules(pid);
        WriteJson(ProcessDiagnostics.GetWindowProcInfo(hwnd, pid, modules));
        return 0;
    }

    private static int Pe(string[] args)
    {
        if (args.Length < 2 || !args[1].Equals("exports", StringComparison.OrdinalIgnoreCase))
            return Fail("Usage: mcgsctl pe exports --file <dll-or-exe>");

        var file = RequiredPath(args, "--file");
        var exports = PeInspector.ReadExports(file);
        if (Opt(args, "--filter") is string filter)
        {
            exports = exports with
            {
                Exports = exports.Exports
                    .Where(e => (e.Name ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                (e.Forwarder ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToArray()
            };
        }
        WriteJson(exports);
        return 0;
    }

    private static int Strings(string[] args)
    {
        var file = RequiredPath(args, "--file");
        var min = ParseInt(args, "--min", 4);
        var limit = ParseInt(args, "--limit", 200);
        var encoding = (Opt(args, "--encoding") ?? "both").ToLowerInvariant();
        var strings = BinaryStrings.Extract(file, min, encoding);
        if (Opt(args, "--filter") is string filter)
        {
            strings = strings
                .Where(s => s.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var returned = limit > 0 ? strings.Take(limit).ToArray() : strings;
        WriteJson(new
        {
            file = Path.GetFullPath(file),
            count = strings.Length,
            returned = returned.Length,
            strings = returned
        });
        return 0;
    }

    private static int SendMsg(string[] args)
    {
        var hwnd = ResolveWindow(args);
        var msg = (uint)ParseNumberRequired(args, "--msg");
        var wParam = new IntPtr(ParseNumber(args, "--wparam", 0));
        var lParam = new IntPtr(ParseNumber(args, "--lparam", 0));
        if (Has(args, "--post"))
        {
            Native.PostMessage(hwnd, msg, wParam, lParam);
            Console.WriteLine("posted");
        }
        else
        {
            var result = Native.SendMessage(hwnd, msg, wParam, lParam);
            WriteJson(new { result = result.ToInt64(), hex = "0x" + result.ToInt64().ToString("X") });
        }
        return 0;
    }

    private static int Notify(string[] args)
    {
        var hwnd = ResolveWindow(args);
        var code = (int)ParseNumberRequired(args, "--code");
        var parent = OptHwnd(args, "--parent");
        var send = Has(args, "--send");
        var delay = ParseInt(args, "--delay", 800);
        UiAutomation.NotifyControl(hwnd, parent == IntPtr.Zero ? null : parent, code, send, delay);
        Console.WriteLine("notify sent");
        return 0;
    }

    private static int Focus(string[] args)
    {
        var hwnd = ResolveWindow(args);
        Native.SetForegroundWindow(hwnd);
        Native.SetFocus(hwnd);
        Console.WriteLine($"focused 0x{hwnd.ToInt64():X}");
        return 0;
    }

    private static int Toolbar(string[] args)
    {
        var hwnd = ResolveWindow(args);
        var buttons = UiAutomation.ToolbarButtons(hwnd);

        if (OptInt(args, "--click-index") is int clickIndex)
        {
            UiAutomation.ClickToolbarButton(hwnd, buttons, b => b.Index == clickIndex, Has(args, "--mouse"));
            Console.WriteLine("toolbar button clicked");
            return 0;
        }

        if (OptInt(args, "--click-id") is int clickId)
        {
            UiAutomation.ClickToolbarButton(hwnd, buttons, b => b.IdCommand == clickId, Has(args, "--mouse"));
            Console.WriteLine("toolbar button clicked");
            return 0;
        }

        if (OptInt(args, "--command-index") is int commandIndex)
        {
            UiAutomation.SendToolbarCommand(hwnd, buttons, b => b.Index == commandIndex);
            Console.WriteLine("toolbar command sent");
            return 0;
        }

        if (OptInt(args, "--command-id") is int commandId)
        {
            UiAutomation.SendToolbarCommand(hwnd, buttons, b => b.IdCommand == commandId);
            Console.WriteLine("toolbar command sent");
            return 0;
        }

        WriteJson(buttons);
        return 0;
    }

    private static int TreeView(string[] args)
    {
        var hwnd = ResolveWindow(args);

        if (Has(args, "--caret"))
        {
            var caret = UiAutomation.TreeViewCaret(hwnd);
            WriteJson(new
            {
                handle = "0x" + caret.ToInt64().ToString("X"),
                text = caret == IntPtr.Zero ? "" : UiAutomation.TreeViewItemText(hwnd, caret),
                rect = caret == IntPtr.Zero ? new Rect(0, 0, 0, 0) : UiAutomation.TreeViewItemRect(hwnd, caret)
            });
            return 0;
        }

        if (Has(args, "--select-root"))
        {
            var root = UiAutomation.TreeViewRoot(hwnd);
            if (root == IntPtr.Zero) return Fail("TreeView root item was not found.");
            UiAutomation.TreeViewSelect(hwnd, root);
            Console.WriteLine("tree root selected");
            return 0;
        }

        if (Has(args, "--expand-root"))
        {
            var root = UiAutomation.TreeViewRoot(hwnd);
            if (root == IntPtr.Zero) return Fail("TreeView root item was not found.");
            UiAutomation.TreeViewExpand(hwnd, root);
            Console.WriteLine("tree root expanded");
            return 0;
        }

        if (Opt(args, "--select") is string selectItem)
        {
            UiAutomation.TreeViewSelect(hwnd, ParseHwnd(selectItem));
            Console.WriteLine("tree item selected");
            return 0;
        }

        if (Opt(args, "--select-text") is string selectText)
        {
            UiAutomation.TreeViewSelectText(hwnd, selectText);
            Console.WriteLine("tree item selected");
            return 0;
        }

        if (Opt(args, "--expand") is string expandItem)
        {
            UiAutomation.TreeViewExpand(hwnd, ParseHwnd(expandItem));
            Console.WriteLine("tree item expanded");
            return 0;
        }

        if (Opt(args, "--expand-text") is string expandText)
        {
            UiAutomation.TreeViewExpandText(hwnd, expandText);
            Console.WriteLine("tree item expanded");
            return 0;
        }

        if (Opt(args, "--notify-selchanged") is string notifyItem)
        {
            UiAutomation.TreeViewNotifySelectionChanged(hwnd, ParseHwnd(notifyItem), Has(args, "--unicode"), Has(args, "--send"));
            Console.WriteLine("tree selection notification sent");
            return 0;
        }

        if (Opt(args, "--notify-selchanged-text") is string notifyText)
        {
            var item = UiAutomation.TreeViewFindItem(hwnd, notifyText);
            UiAutomation.TreeViewNotifySelectionChanged(hwnd, ParseHwnd(item.Handle), Has(args, "--unicode"), Has(args, "--send"));
            Console.WriteLine("tree selection notification sent");
            return 0;
        }

        if (Opt(args, "--rect") is string rectItem)
        {
            WriteJson(UiAutomation.TreeViewItemRect(hwnd, ParseHwnd(rectItem)));
            return 0;
        }

        if (Opt(args, "--text") is string textItem)
        {
            Console.WriteLine(UiAutomation.TreeViewItemText(hwnd, ParseHwnd(textItem)));
            return 0;
        }

        WriteJson(UiAutomation.TreeViewItems(hwnd));
        return 0;
    }

    private static int ListView(string[] args)
    {
        var hwnd = ResolveWindow(args);
        if (OptInt(args, "--select-index") is int selectIndex)
        {
            UiAutomation.ListViewSelectIndex(hwnd, selectIndex);
            Console.WriteLine("listview selection updated");
            return 0;
        }

        if (Opt(args, "--select-text") is string selectText)
        {
            UiAutomation.ListViewSelectText(hwnd, selectText);
            Console.WriteLine("listview selection updated");
            return 0;
        }

        if (OptInt(args, "--double-index") is int doubleIndex)
        {
            UiAutomation.ListViewDoubleClickIndex(hwnd, doubleIndex, Has(args, "--mouse"));
            Console.WriteLine("listview item double clicked");
            return 0;
        }

        if (Opt(args, "--double-text") is string doubleText)
        {
            UiAutomation.ListViewDoubleClickText(hwnd, doubleText, Has(args, "--mouse"));
            Console.WriteLine("listview item double clicked");
            return 0;
        }

        WriteJson(UiAutomation.ListViewItems(hwnd));
        return 0;
    }

    private static int Combo(string[] args)
    {
        var hwnd = ResolveWindow(args);
        if (Has(args, "--items"))
        {
            WriteJson(UiAutomation.ComboItems(hwnd));
            return 0;
        }
        var selected = false;
        if (OptInt(args, "--select-index") is int index)
        {
            UiAutomation.ComboSelectIndex(hwnd, index);
            selected = true;
        }
        var text = Opt(args, "--select-text");
        if (!string.IsNullOrWhiteSpace(text))
        {
            UiAutomation.ComboSelectText(hwnd, text);
            selected = true;
        }
        if (!selected) WriteJson(new { currentIndex = UiAutomation.ComboCurrentIndex(hwnd), items = UiAutomation.ComboItems(hwnd) });
        else Console.WriteLine("combo updated");
        return 0;
    }

    private static int List(string[] args)
    {
        var hwnd = ResolveWindow(args);
        if (Has(args, "--items"))
        {
            WriteJson(UiAutomation.ListBoxItems(hwnd));
            return 0;
        }
        if (OptInt(args, "--select-index") is int index)
        {
            UiAutomation.ListBoxSelectIndex(hwnd, index);
            Console.WriteLine("list selection updated");
            return 0;
        }
        if (Opt(args, "--select-text") is string text)
        {
            UiAutomation.ListBoxSelectText(hwnd, text);
            Console.WriteLine("list selection updated");
            return 0;
        }
        WriteJson(new { currentIndex = UiAutomation.ListBoxCurrentIndex(hwnd), items = UiAutomation.ListBoxItems(hwnd) });
        return 0;
    }

    private static int Tab(string[] args)
    {
        var hwnd = ResolveWindow(args);
        if (OptInt(args, "--select-index") is int index)
        {
            UiAutomation.TabSelectIndex(hwnd, index, Has(args, "--mouse"));
            Console.WriteLine("tab selected");
            return 0;
        }

        if (Opt(args, "--select-text") is string text)
        {
            UiAutomation.TabSelectText(hwnd, text, Has(args, "--mouse"));
            Console.WriteLine("tab selected");
            return 0;
        }

        WriteJson(UiAutomation.TabItems(hwnd));
        return 0;
    }

    private static int CheckControl(string[] args)
    {
        var hwnd = ResolveWindow(args);
        if (Has(args, "--toggle"))
        {
            UiAutomation.ButtonToggleCheck(hwnd);
        }
        else
        {
            UiAutomation.ButtonSetCheck(hwnd, Has(args, "--on"));
        }
        WriteJson(new { checkedState = UiAutomation.ButtonGetCheck(hwnd) });
        return 0;
    }

    private static int Click(string[] args)
    {
        var text = Required(args, "--text");
        var hwnd = ResolveWindow(args);
        var clicked = UiAutomation.ClickButtonByText(hwnd, text, Has(args, "--mouse"));
        if (!clicked) return Fail("Button not found: " + text);
        Console.WriteLine("clicked: " + text);
        return 0;
    }

    private static int Point(string[] args)
    {
        var hwnd = ResolveWindow(args);
        var x = ParseIntRequired(args, "--x");
        var y = ParseIntRequired(args, "--y");
        var button = Has(args, "--right") ? MouseButton.Right : MouseButton.Left;
        UiAutomation.ClickPoint(hwnd, x, y, button, Has(args, "--double"), Has(args, "--mouse"));
        Console.WriteLine($"clicked {button} at {x},{y} on 0x{hwnd.ToInt64():X}");
        return 0;
    }

    private static int Drag(string[] args)
    {
        var hwnd = ResolveWindow(args);
        var x1 = ParseIntRequired(args, "--x1");
        var y1 = ParseIntRequired(args, "--y1");
        var x2 = ParseIntRequired(args, "--x2");
        var y2 = ParseIntRequired(args, "--y2");
        UiAutomation.DragPoint(hwnd, x1, y1, x2, y2, Has(args, "--mouse"));
        Console.WriteLine($"dragged {x1},{y1} to {x2},{y2} on 0x{hwnd.ToInt64():X}");
        return 0;
    }

    private static int SetText(string[] args)
    {
        var hwnd = ResolveWindow(args);
        var text = Opt(args, "--text");
        var file = Opt(args, "--file");
        if (text == null && file == null) return Fail("Use --text or --file.");
        if (file != null) text = File.ReadAllText(FullPath(file), Encoding.UTF8);
        UiAutomation.SetControlText(hwnd, text ?? "", Has(args, "--paste"));
        Console.WriteLine($"set text on 0x{hwnd.ToInt64():X}");
        return 0;
    }

    private static int GetText(string[] args)
    {
        var hwnd = ResolveWindow(args);
        Console.WriteLine(Native.GetText(hwnd));
        return 0;
    }

    private static int Keys(string[] args)
    {
        var text = Required(args, "--text");
        SendKeys.SendWait(text);
        Console.WriteLine("sent keys");
        return 0;
    }

    private static int Wait(string[] args)
    {
        var pid = OptInt(args, "--pid");
        var title = Opt(args, "--title");
        var className = Opt(args, "--class");
        var timeout = TimeSpan.FromSeconds(ParseInt(args, "--timeout", 10));
        var hwnd = UiAutomation.WaitForWindow(pid, title, className, timeout);
        if (hwnd == IntPtr.Zero) return Fail("Timed out waiting for matching window.");
        WriteJson(WindowInfo.FromHandle(hwnd));
        return 0;
    }

    private static int Popup(string[] args)
    {
        var openHwnd = OptHwnd(args, "--open-hwnd");
        if (openHwnd != IntPtr.Zero)
        {
            var x = ParseIntRequired(args, "--x");
            var y = ParseIntRequired(args, "--y");
            if (Has(args, "--context"))
                UiAutomation.OpenContextMenu(openHwnd, x, y);
            else
                UiAutomation.ClickPoint(openHwnd, x, y, MouseButton.Right, false, Has(args, "--mouse"));
            Thread.Sleep(ParseInt(args, "--delay", 350));
        }

        var pid = OptInt(args, "--pid");
        if (!pid.HasValue && openHwnd != IntPtr.Zero) pid = UiAutomation.GetWindowProcessId(openHwnd);
        var popups = UiAutomation.GetPopupMenus(pid).ToArray();
        var chooseIndex = OptInt(args, "--choose-index");
        var chooseId = OptInt(args, "--choose-id");
        var chooseText = Opt(args, "--choose-text");
        if ((chooseIndex.HasValue || chooseId.HasValue || !string.IsNullOrWhiteSpace(chooseText)) && popups.Length > 0)
        {
            UiAutomation.ChoosePopupItem(popups[0], chooseIndex, chooseId, chooseText, Has(args, "--exact"), Has(args, "--mouse"));
            Thread.Sleep(ParseInt(args, "--after", 500));
        }
        WriteJson(popups);
        return 0;
    }

    private static int RunScript(string[] args)
    {
        var file = RequiredPath(args, "--file");
        var runner = new ActionRunner(OptInt(args, "--pid"));
        runner.Run(file);
        return 0;
    }

    private static int Capture(string[] args)
    {
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "capture-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        var pid = OptInt(args, "--pid");
        var windows = pid.HasValue
            ? UiAutomation.TopWindowsForPid(pid.Value).ToArray()
            : UiAutomation.AllMcgsWindows().ToArray();
        var infos = new List<WindowInfo>();

        for (var i = 0; i < windows.Length; i++)
        {
            var hwnd = windows[i];
            var info = WindowInfo.FromHandle(hwnd);
            infos.Add(info);
            var prefix = $"{i:D2}-{SafeFile(info.ClassName)}-{SafeFile(info.Text)}";
            if (prefix.Length > 90) prefix = prefix[..90];
            File.WriteAllLines(Path.Combine(outDir, prefix + ".tree.txt"), UiAutomation.WindowTreeLines(hwnd), Encoding.UTF8);
            TryScreenshot(hwnd, Path.Combine(outDir, prefix + ".png"));
        }

        File.WriteAllText(Path.Combine(outDir, "windows.json"), JsonSerializer.Serialize(infos, JsonOptions()), Encoding.UTF8);
        Console.WriteLine(outDir);
        return 0;
    }

    private static int Close(string[] args)
    {
        var save = Has(args, "--save");
        var discard = Has(args, "--discard");
        if (!save && !discard) return Fail("Use --save or --discard.");
        var hwnd = ResolveMainWindow(args);
        var pid = UiAutomation.GetWindowProcessId(hwnd);

        if (Native.GetClass(hwnd) != "#32770" && save)
        {
            UiAutomation.SendCommand(hwnd, SaveCommandId);
            Thread.Sleep(700);
        }

        if (Native.GetClass(hwnd) != "#32770")
        {
            UiAutomation.CloseWindow(hwnd);
        }
        var closed = UiAutomation.HandleCloseDialogs(pid, save, TimeSpan.FromSeconds(12));
        if (!closed) return Fail("Editor did not close before timeout.");
        Console.WriteLine("closed");
        return 0;
    }

    private static int Snapshot(string[] args)
    {
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", Timestamp()));
        Directory.CreateDirectory(outDir);

        string? project = Opt(args, "--project");
        string? mceExportError = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            project = FullPath(project);
            if (!File.Exists(project)) return Fail("Project not found: " + project);
            var mceOut = Path.Combine(outDir, "mce");
            try
            {
                MceExporter.Export(project, mceOut);
            }
            catch (Exception ex)
            {
                mceExportError = ex.Message;
                Directory.CreateDirectory(mceOut);
                File.WriteAllText(Path.Combine(mceOut, "mce-error.txt"), ex.ToString(), Encoding.UTF8);
            }
        }

        int? pid = OptInt(args, "--pid");
        IntPtr hwnd = IntPtr.Zero;
        int? activePid = null;
        int? moduleCount = null;
        if (pid.HasValue || UiAutomation.TryFindLatestMainWindow(out hwnd))
        {
            if (pid.HasValue) hwnd = ResolveWindow(args);
            activePid = UiAutomation.GetWindowProcessId(hwnd);
            File.WriteAllLines(Path.Combine(outDir, "window-tree.txt"), UiAutomation.WindowTreeLines(hwnd), Encoding.UTF8);
            File.WriteAllLines(Path.Combine(outDir, "menus.txt"),
                UiAutomation.GetMenus(hwnd).Select(m => m.Id.HasValue ? $"{m.Id.Value}\t{m.Path}" : $"-\t{m.Path}"),
                Encoding.UTF8);
            TryScreenshot(hwnd, Path.Combine(outDir, "window.png"));

            var modules = ProcessDiagnostics.GetModules(activePid.Value);
            moduleCount = modules.Length;
            File.WriteAllText(Path.Combine(outDir, "modules.json"), JsonSerializer.Serialize(modules, JsonOptions()), Encoding.UTF8);
            File.WriteAllText(Path.Combine(outDir, "mdi.json"), JsonSerializer.Serialize(UiAutomation.GetMdiInfo(hwnd), JsonOptions()), Encoding.UTF8);
            File.WriteAllText(Path.Combine(outDir, "guiinfo.json"), JsonSerializer.Serialize(UiAutomation.GetGuiInfo(hwnd), JsonOptions()), Encoding.UTF8);

            var wndprocs = UiAutomation.TopWindowsForPid(activePid.Value)
                .Select(w => ProcessDiagnostics.GetWindowProcInfo(w, activePid.Value, modules))
                .ToArray();
            File.WriteAllText(Path.Combine(outDir, "wndprocs.json"), JsonSerializer.Serialize(wndprocs, JsonOptions()), Encoding.UTF8);
        }

        var snapshot = new Dictionary<string, object?>
        {
            ["createdAt"] = DateTimeOffset.Now.ToString("O"),
            ["project"] = project,
            ["projectSha256"] = TrySha256(project, out var shaError),
            ["projectSha256Error"] = shaError,
            ["pid"] = activePid,
            ["mainWindow"] = hwnd != IntPtr.Zero ? WindowInfo.FromHandle(hwnd) : null,
            ["moduleCount"] = moduleCount,
            ["mceExportError"] = mceExportError,
            ["outDir"] = outDir
        };
        File.WriteAllText(Path.Combine(outDir, "snapshot.json"), JsonSerializer.Serialize(snapshot, JsonOptions()), Encoding.UTF8);
        Console.WriteLine(outDir);
        return 0;
    }

    private static int Mce(string[] args)
    {
        if (args.Length < 2 || !args[1].Equals("export", StringComparison.OrdinalIgnoreCase))
            return Fail("Usage: mcgsctl mce export --project <mce> [--out <dir>]");

        var project = RequiredPath(args, "--project");
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "mce-" + Timestamp()));
        MceExporter.Export(project, outDir);
        Console.WriteLine(outDir);
        return 0;
    }

    private static int Verify(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var spec = RequiredPath(args, "--spec");
        var outDir = Path.Combine(Environment.CurrentDirectory, ".mcgsctl-cache", "verify-" + Timestamp());
        MceExporter.Export(project, outDir);

        var dataText = File.ReadAllText(Path.Combine(outDir, "data.json"), Encoding.UTF8);
        var blobText = File.ReadAllText(Path.Combine(outDir, "blob_strings.json"), Encoding.UTF8);
        var summaryText = File.ReadAllText(Path.Combine(outDir, "summary.json"), Encoding.UTF8);
        using var specDoc = JsonDocument.Parse(File.ReadAllText(spec, Encoding.UTF8));
        var failed = 0;

        if (specDoc.RootElement.TryGetProperty("variables", out var variables) && variables.ValueKind == JsonValueKind.Array)
        {
            foreach (var variable in variables.EnumerateArray())
            {
                foreach (var expectation in FlattenExpectations(variable))
                {
                    Result("variable contains " + expectation, dataText.Contains(expectation, StringComparison.Ordinal), ref failed);
                }
            }
        }

        if (specDoc.RootElement.TryGetProperty("blobContains", out var blobContains) && blobContains.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in blobContains.EnumerateArray())
            {
                var expected = item.GetString() ?? "";
                Result("blob contains " + expected, blobText.Contains(expected, StringComparison.Ordinal), ref failed);
            }
        }

        if (specDoc.RootElement.TryGetProperty("tables", out var tables) && tables.ValueKind == JsonValueKind.Object)
        {
            var rows = SummaryRows(summaryText);
            foreach (var table in tables.EnumerateObject())
            {
                var expectedRows = table.Value.GetInt32();
                Result($"table {table.Name} rows == {expectedRows}",
                    rows.TryGetValue(table.Name, out var actualRows) && actualRows == expectedRows, ref failed);
            }
        }

        Console.WriteLine("evidence: " + outDir);
        return failed == 0 ? 0 : 1;
    }

    private static int Workflow(string[] args)
    {
        if (args.Length < 3 || !args[1].Equals("run", StringComparison.OrdinalIgnoreCase))
            return Fail("Usage: mcgsctl workflow run <name> [--args ...]");

        var name = args[2];
        if (name.Equals("project.check", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowProjectCheck(args, saveAfterPass: false);
        }

        if (name.Equals("project.check-save", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowProjectCheck(args, saveAfterPass: true);
        }

        if (name.Equals("project.apply-candidate", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowProjectApplyCandidate(args);
        }

        if (name.Equals("project.rollback", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowProjectRollback(args);
        }

        if (name.Equals("safety.verify", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowSafetyVerify(args);
        }

        if (name.Equals("window.layout.apply", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowLayoutApply(args);
        }

        if (name.Equals("window.button.add-momentary", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowAddMomentaryButton(args);
        }

        if (name.Equals("window.static-text.add", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowNativeStaticTextAdd(args);
        }

        if (name.Equals("window.lamp.add-native", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowNativeLampAdd(args);
        }

        if (name.Equals("realtime-db.add", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowRealtimeDbAdd(args);
        }

        if (name.Equals("device.channel.map", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowDeviceChannelMap(args);
        }

        if (name.Equals("script.edit", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowScriptEdit(args);
        }

        if (name.Equals("window.indicator.add", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowWindowIndicatorAdd(args);
        }

        var supportedLater = Array.Empty<string>();
        if (supportedLater.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine($"{name} is registered but not enabled yet.");
            Console.Error.WriteLine("This workflow needs one recorded MCGS dialog profile before it can safely write through the GUI.");
            return 2;
        }

        return Fail("Unknown workflow: " + name);
    }

    private sealed record ProjectCheckResult(
        bool Passed,
        bool Unknown,
        int ErrorCount,
        int WarningCount,
        string[] DialogTexts,
        string[] DialogTitles,
        string[] ResultRows,
        int ResultControlCount,
        bool ResultControlsReadable,
        string VerdictReason);

    private static int WorkflowProjectCheck(string[] args, bool saveAfterPass)
    {
        var workflowName = saveAfterPass ? "project.check-save" : "project.check";
        var projectOpt = Opt(args, "--project");
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", workflowName + "-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        SetDialogEvidenceRoot(outDir);

        WorkflowProjectContext? workflowProject = null;
        Process? process = null;
        IntPtr hwnd = IntPtr.Zero;
        string? project = null;
        string? checkProject = null;
        bool checkedTemporaryCopy = false;
        string? checkedProjectSha256Before = null;
        var saved = false;
        var success = false;

        try
        {
            if (OptInt(args, "--pid").HasValue)
            {
                if (!Has(args, "--allow-attached"))
                    return Fail(workflowName + " with --pid requires --allow-attached because it can act on an already-open editor instance.");
                hwnd = ResolveMainWindow(args);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(projectOpt) && string.IsNullOrWhiteSpace(Opt(args, "--source")))
                    return Fail(workflowName + " requires --source, --project, or --pid.");

                workflowProject = PrepareWorkflowProject(args, workflowName, outDir);
                project = workflowProject.Project;
                checkProject = project;
                if (!saveAfterPass)
                {
                    checkProject = CreateProjectCheckCopy(workflowProject, outDir, out checkedProjectSha256Before);
                    checkedTemporaryCopy = true;
                }
                var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
                process = Process.Start(new ProcessStartInfo(editor, Quote(checkProject))
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
                });
                if (process == null) return Fail("Failed to open editor.");
                hwnd = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
                HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
                hwnd = UiAutomation.FindMainWindow(process.Id);
                if (hwnd == IntPtr.Zero) throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");
            }

            var pid = UiAutomation.GetWindowProcessId(hwnd);
            var failOnWarning = Has(args, "--fail-on-warning");
            var maxWarnings = OptInt(args, "--max-warnings");
            var check = RunProjectCheck(pid, hwnd, outDir, TimeSpan.FromSeconds(ParseInt(args, "--check-timeout", 8)),
                failOnWarning, maxWarnings);
            if (checkedTemporaryCopy && process != null && !process.HasExited)
            {
                CloseEditorProcess(process.Id, hwnd, saveIntent: false);
                try { process.WaitForExit(5000); } catch { }
                process = null;
                hwnd = IntPtr.Zero;
            }

            var finalChecks = ProjectCheckChecks(check).ToList();
            var finalExtra = ProjectCheckExtra(check);
            AddProjectCheckCopyEvidence(workflowProject, checkedTemporaryCopy, checkProject,
                checkedProjectSha256Before, finalChecks, finalExtra);

            if (!check.Passed)
            {
                if (workflowProject != null)
                    WriteFinalValidatorResult(workflowProject, "project-check/check-result.json", "project-check", "project.check",
                        finalChecks, extra: finalExtra);
                WriteWorkflowAuditEndIfNeeded(outDir, workflowProject, saved, success);
                Console.WriteLine("workflow evidence: " + outDir);
                Console.WriteLine(check.Unknown
                    ? "project check verification: UNKNOWN - " + check.VerdictReason
                    : "project check verification: FAIL - " + check.VerdictReason);
                return 1;
            }

            if (saveAfterPass)
            {
                CloseProjectCheckDialogs(pid);
                UiAutomation.SendCommand(hwnd, SaveCommandId);
                Thread.Sleep(1500);
                saved = true;
                if (project != null && File.Exists(project))
                    MceExporter.Export(project, Path.Combine(outDir, "mce"));
            }

            success = true;
            if (workflowProject != null)
                WriteFinalValidatorResult(workflowProject, "project-check/check-result.json", "project-check", "project.check",
                    finalChecks, extra: finalExtra);
            WriteWorkflowAuditEndIfNeeded(outDir, workflowProject, saved, success);
            Console.WriteLine("workflow evidence: " + outDir);
            var warningText = check.WarningCount > 0 ? $" with {check.WarningCount} warning(s)" : "";
            Console.WriteLine((saveAfterPass ? "project check-save verification: PASS" : "project check verification: PASS") + warningText);
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            if (process != null)
            {
                try { CaptureProcessWindows(process.Id, Path.Combine(outDir, "failure")); } catch { }
            }
            WriteWorkflowAuditEndIfNeeded(outDir, workflowProject, saved, success);
            Console.Error.WriteLine("workflow failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, hwnd, saveAfterPass && saved); } catch { }
            }
        }
    }

    private static string CreateProjectCheckCopy(WorkflowProjectContext context, string outDir, out string copySha256)
    {
        var copyDir = Path.Combine(outDir, "project-check-copy");
        Directory.CreateDirectory(copyDir);
        var copy = Path.Combine(copyDir, "candidate-check.MCE");
        File.Copy(context.Project, copy, overwrite: true);
        copySha256 = Sha256(copy);
        if (!copySha256.Equals(context.ProjectSha256Before, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Project check copy SHA256 does not match candidate SHA256 before check.");
        File.WriteAllText(Path.Combine(copyDir, "candidate-check.sha256"),
            copySha256 + "  candidate-check.MCE" + Environment.NewLine, Encoding.UTF8);
        return copy;
    }

    private static void AddProjectCheckCopyEvidence(
        WorkflowProjectContext? context,
        bool checkedTemporaryCopy,
        string? checkProject,
        string? checkedProjectSha256Before,
        List<ResultCheck> checks,
        Dictionary<string, object?> extra)
    {
        if (!checkedTemporaryCopy || context == null || string.IsNullOrWhiteSpace(checkProject)) return;

        var checkedProjectSha256After = TrySha256(checkProject, out var checkedProjectSha256AfterError);
        var candidateSha256After = TrySha256(context.Project, out var candidateSha256AfterError);
        extra["checkedTemporaryCopy"] = true;
        extra["checkedProject"] = checkProject;
        extra["checkedProjectSha256Before"] = checkedProjectSha256Before;
        extra["checkedProjectSha256After"] = checkedProjectSha256After;
        extra["checkedProjectSha256AfterError"] = checkedProjectSha256AfterError;
        extra["candidateSha256DuringCheck"] = candidateSha256After;
        extra["candidateSha256DuringCheckError"] = candidateSha256AfterError;

        if (string.IsNullOrWhiteSpace(candidateSha256After))
        {
            checks.Add(RequiredUnknown("candidate-unchanged-during-project-check",
                candidateSha256AfterError ?? "candidate SHA could not be read after project.check"));
            return;
        }

        if (candidateSha256After.Equals(context.ProjectSha256Before, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(RequiredPass("candidate-unchanged-during-project-check",
                "project.check ran against a temporary copy; candidate SHA was unchanged"));
        }
        else
        {
            checks.Add(RequiredFail("candidate-unchanged-during-project-check",
                "candidate SHA changed during project.check final validation"));
        }
    }

    private static ProjectCheckResult RunProjectCheck(int pid, IntPtr main, string outDir, TimeSpan timeout,
        bool failOnWarning, int? maxWarnings)
    {
        var before = UiAutomation.TopWindowsForPid(pid).ToHashSet();
        UiAutomation.SendCommand(main, CheckCommandId);
        var until = DateTime.UtcNow + timeout;
        ProjectCheckResult result;
        do
        {
            Thread.Sleep(500);
            HandleProjectCheckPrompt(pid);
            result = AnalyzeProjectCheck(pid, before, failOnWarning, maxWarnings);
            if (!result.Unknown) break;
        } while (DateTime.UtcNow < until);

        CaptureProcessWindows(pid, Path.Combine(outDir, "project-check"));
        File.WriteAllText(Path.Combine(outDir, "check-result.json"),
            JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
        File.WriteAllLines(Path.Combine(outDir, "window-tree.txt"), UiAutomation.WindowTreeLines(main), Encoding.UTF8);
        TryScreenshot(main, Path.Combine(outDir, "window.png"));
        return result;
    }

    private static void HandleProjectCheckPrompt(int pid)
    {
        foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h => Native.GetClass(h) == "#32770"))
        {
            var text = DialogText(dialog);
            if (!ContainsAny(text, "是否检查所有窗口", "用户程序", "表达式")) continue;
            RecordDialogEvidence("dialogs.jsonl", pid, dialog, "click-ok", "project-check.prompt");
            ClickButtonByNormalizedText(dialog, mouse: true, "确定", "确认", "是");
            Thread.Sleep(500);
        }
        RecordOpenPopups(pid, "project-check.prompt");
    }

    private static ProjectCheckResult AnalyzeProjectCheck(int pid, HashSet<IntPtr> before,
        bool failOnWarning, int? maxWarnings)
    {
        var dialogs = UiAutomation.TopWindowsForPid(pid)
            .Where(h => Native.GetClass(h) == "#32770")
            .Where(h => !before.Contains(h) || DialogText(h).Contains("检查", StringComparison.OrdinalIgnoreCase) ||
                         DialogText(h).Contains("错误", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var texts = dialogs.Select(DialogText).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        var titles = dialogs.Select(Native.GetText).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        var resultRows = ReadProjectCheckResultRows(dialogs, out var resultControlCount, out var resultControlsReadable);
        var combined = string.Join("\n", texts.Concat(resultRows));
        var errorCount = ParseIssueCount(combined, "错误", "error", "errors");
        var warningCount = ParseIssueCount(combined, "警告", "warning", "warnings");
        var hasFatalText = IsDialogErrorText(combined) || ContainsAny(combined, "失败", "不通过");
        var explicitZeroErrors = ContainsAny(combined, "0个错误", "0 个错误", "无错误", "没有错误");
        var explicitPass = ContainsAny(combined, "检查通过");
        var readableEmptyResultList = resultControlCount > 0 && resultControlsReadable && resultRows.Length == 0;
        var known = texts.Length > 0 || resultControlCount > 0;

        string reason;
        var passed = false;
        var unknown = false;
        if (!known)
        {
            unknown = true;
            reason = "no project-check result dialog was found";
        }
        else if (!resultControlsReadable)
        {
            unknown = true;
            reason = "project-check result list exists but could not be read";
        }
        else if (hasFatalText || errorCount > 0)
        {
            reason = $"error count is {errorCount}";
        }
        else if (failOnWarning && warningCount > 0)
        {
            reason = $"warning count is {warningCount} and --fail-on-warning is set";
        }
        else if (maxWarnings.HasValue && warningCount > maxWarnings.Value)
        {
            reason = $"warning count {warningCount} exceeds --max-warnings {maxWarnings.Value}";
        }
        else if (explicitZeroErrors || explicitPass || readableEmptyResultList)
        {
            passed = true;
            reason = explicitZeroErrors ? "explicit zero-error text found" :
                explicitPass ? "explicit pass text found" :
                "readable result list has zero rows";
        }
        else
        {
            unknown = true;
            reason = "no explicit zero-error/pass text and no readable empty result list";
        }

        return new ProjectCheckResult(passed, unknown, errorCount, warningCount, texts, titles,
            resultRows, resultControlCount, resultControlsReadable, reason);
    }

    private static string[] ReadProjectCheckResultRows(IEnumerable<IntPtr> dialogs, out int controlCount, out bool readable)
    {
        var rows = new List<string>();
        controlCount = 0;
        readable = true;
        foreach (var dialog in dialogs)
        {
            foreach (var child in UiAutomation.EnumerateChildren(dialog))
            {
                var className = Native.GetClass(child);
                try
                {
                    if (className.Contains("SysListView32", StringComparison.OrdinalIgnoreCase))
                    {
                        controlCount++;
                        rows.AddRange(UiAutomation.ListViewItems(child)
                            .Select(item => string.Join("\t", item.Texts.Where(t => !string.IsNullOrWhiteSpace(t))))
                            .Where(text => !string.IsNullOrWhiteSpace(text)));
                    }
                    else if (className.Contains("ListBox", StringComparison.OrdinalIgnoreCase))
                    {
                        controlCount++;
                        rows.AddRange(UiAutomation.ListBoxItems(child)
                            .Select(item => item.Text)
                            .Where(text => !string.IsNullOrWhiteSpace(text)));
                    }
                }
                catch
                {
                    readable = false;
                }
            }
        }
        return rows.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int ParseIssueCount(string text, params string[] labels)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var max = 0;
        foreach (var label in labels)
        {
            foreach (Match match in Regex.Matches(text, @"(\d+)\s*(?:个|條|条)?\s*" + Regex.Escape(label), RegexOptions.IgnoreCase))
            {
                if (int.TryParse(match.Groups[1].Value, out var value)) max = Math.Max(max, value);
            }
        }
        if (max == 0 && labels.Any(label => text.Contains(label, StringComparison.OrdinalIgnoreCase)) &&
            !ContainsAny(text, "0个", "0 个", "无", "没有"))
        {
            max = 1;
        }
        return max;
    }

    private static void CloseProjectCheckDialogs(int pid)
    {
        foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h => Native.GetClass(h) == "#32770"))
        {
            var text = DialogText(dialog);
            if (!ContainsAny(text, "检查", "错误", "警告", "成功", "完成")) continue;
            RecordDialogEvidence("dialogs.jsonl", pid, dialog, "close", "project-check.close-result");
            ClickButtonByNormalizedText(dialog, mouse: true, "确定", "确认", "关闭", "关闭(&C)");
            Thread.Sleep(300);
        }
        RecordOpenPopups(pid, "project-check.close-result");
    }

    private static void WriteWorkflowAuditEndIfNeeded(string outDir, WorkflowProjectContext? context, bool saved, bool success)
    {
        if (context != null) WriteWorkflowAuditEnd(outDir, context, saved, success);
    }

    private static int WorkflowRealtimeDbAdd(string[] args)
    {
        var objectName = Required(args, "--name");
        var rawType = Opt(args, "--type") ?? "switch";
        var typeText = NormalizeRealtimeType(rawType);
        var expectedTypeCode = RealtimeTypeCode(rawType);
        if (expectedTypeCode == null)
            return Fail("Realtime DB type verification is not implemented for --type " + rawType + ". Supported verified types: switch, numeric.");
        var initial = Opt(args, "--initial") ?? "0";
        var unit = Opt(args, "--unit") ?? "";
        var note = Opt(args, "--note") ?? "";
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "realtime-db-add-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        var workflowProject = PrepareWorkflowProject(args, "realtime-db.add", outDir);
        var project = workflowProject.Project;

        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        var saved = false;
        var success = false;

        try
        {
            var beforeSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-before"));
            var beforeMatches = beforeSnapshot.FindDataObjects(objectName);
            if (beforeMatches.Length > 0)
                throw new InvalidOperationException("Data object already exists before workflow: " + objectName);

            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) return Fail("Failed to open editor.");
            main = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");

            UiAutomation.SendCommand(main, 33957);
            Thread.Sleep(800);
            if (!UiAutomation.ClickButtonByText(main, "新增对象", mouse: true))
                throw new InvalidOperationException("新增对象 button was not found.");
            Thread.Sleep(600);
            if (!UiAutomation.ClickButtonByText(main, "对象属性", mouse: true))
                throw new InvalidOperationException("对象属性 button was not found.");
            var dialog = UiAutomation.WaitForWindow(process.Id, "数据对象属性设置", "#32770", TimeSpan.FromSeconds(8));
            if (dialog == IntPtr.Zero) throw new TimeoutException("数据对象属性设置 dialog was not found.");
            Thread.Sleep(300);

            UiAutomation.SetControlText(FindRealtimeDbEdit(dialog, "name"), objectName, paste: true);
            UiAutomation.SetControlText(FindRealtimeDbEdit(dialog, "initial"), initial, paste: true);
            if (!string.IsNullOrWhiteSpace(unit))
                UiAutomation.SetControlText(FindRealtimeDbEdit(dialog, "unit"), unit, paste: true);
            if (!string.IsNullOrWhiteSpace(note))
                UiAutomation.SetControlText(FindRealtimeDbEdit(dialog, "note"), note, paste: true);
            if (!UiAutomation.ClickButtonByText(dialog, typeText, mouse: true))
                throw new InvalidOperationException("Data object type button was not found: " + typeText);

            CaptureProcessWindows(process.Id, Path.Combine(outDir, "configured-before-confirm"));
            if (!UiAutomation.ClickButtonByText(dialog, "确认", mouse: true))
                throw new InvalidOperationException("确认 button was not found.");
            Thread.Sleep(1000);

            UiAutomation.SendCommand(main, SaveCommandId);
            Thread.Sleep(2000);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "after-save"));
            var afterSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-after"));
            var afterMatches = afterSnapshot.FindDataObjects(objectName);
            var addedRow = afterMatches.LastOrDefault();
            var nameDelta = afterMatches.Length - beforeMatches.Length;
            var initialMatches = addedRow != null && SameOptionalText(addedRow.Initial, initial);
            var unitMatches = addedRow != null && SameOptionalText(addedRow.Unit, unit);
            var noteMatches = addedRow != null && SameOptionalText(addedRow.Note, note);
            var typeMatches = expectedTypeCode == null || (addedRow != null && addedRow.DataType == expectedTypeCode);
            var found = beforeMatches.Length == 0 &&
                        afterMatches.Length == 1 &&
                        nameDelta == 1 &&
                        initialMatches &&
                        unitMatches &&
                        noteMatches &&
                        typeMatches;
            saved = true;
            CloseEditorProcess(process.Id, main, saveIntent: true);
            process = null;
            main = IntPtr.Zero;
            var reopenSnapshot = ReopenProjectAndExportSnapshot(project, editor, outDir,
                "reopen-verify", "mce-reopen", TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            var reopenMatches = reopenSnapshot.FindDataObjects(objectName);
            var reopenedRow = reopenMatches.LastOrDefault();
            var reopenVerified = found &&
                                 reopenMatches.Length == 1 &&
                                 reopenedRow != null &&
                                 SameOptionalText(reopenedRow.Initial, initial) &&
                                 SameOptionalText(reopenedRow.Unit, unit) &&
                                 SameOptionalText(reopenedRow.Note, note) &&
                                 (expectedTypeCode == null || reopenedRow.DataType == expectedTypeCode);
            File.WriteAllText(Path.Combine(outDir, "result.json"),
                JsonSerializer.Serialize(new
                {
                    project,
                    name = objectName,
                    rawType,
                    type = typeText,
                    expectedTypeCode,
                    initial,
                    unit,
                    note,
                    beforeCount = beforeMatches.Length,
                    afterCount = afterMatches.Length,
                    nameDelta,
                    addedRow,
                    initialMatches,
                    unitMatches,
                    noteMatches,
                    typeMatches,
                    reopenCount = reopenMatches.Length,
                    reopenedRow,
                    reopenVerified,
                    found
                }, JsonOptions()),
                Encoding.UTF8);

            success = found && reopenVerified;
            WriteMutatingWorkflowResult(workflowProject, outDir, new[]
            {
                found ? RequiredPass("data-object-delta", objectName) : RequiredFail("data-object-delta", "Data object delta or fields did not match."),
                reopenVerified ? RequiredPass("reopen-readback", objectName) : RequiredFail("reopen-readback", "Reopened Data table did not match expected object fields.")
            }, extra: new Dictionary<string, object?>
            {
                ["touchedDataObjects"] = new[] { objectName },
                ["createdDataObjects"] = new[] { objectName },
                ["modifiedDataObjects"] = Array.Empty<string>(),
                ["controlEvidence"] = Array.Empty<object>()
            });
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.WriteLine("workflow evidence: " + outDir);
            Console.WriteLine(success ? "realtime db add verification: PASS" : "realtime db add verification: CHECK EVIDENCE");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            if (process != null)
            {
                try { CaptureProcessWindows(process.Id, Path.Combine(outDir, "failure")); } catch { }
            }
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.Error.WriteLine("workflow failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    var hwnd = main != IntPtr.Zero ? main : UiAutomation.FindMainWindow(process.Id);
                    foreach (var dialog in UiAutomation.TopWindowsForPid(process.Id).Where(h => h != hwnd && Native.GetClass(h) == "#32770"))
                    {
                        UiAutomation.CloseWindow(dialog);
                        Thread.Sleep(200);
                    }
                    if (hwnd != IntPtr.Zero)
                    {
                        if (saved) UiAutomation.SendCommand(hwnd, SaveCommandId);
                        Thread.Sleep(500);
                        UiAutomation.CloseWindow(hwnd);
                        UiAutomation.HandleCloseDialogs(process.Id, saved, TimeSpan.FromSeconds(12));
                    }
                }
                catch { }
            }
        }
    }

    private static string NormalizeRealtimeType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "switch" or "bool" or "boolean" or "bit" or "开关" => "开关",
            "numeric" or "number" or "float" or "int" or "数值" => "数值",
            "string" or "char" or "字符" => "字符",
            "event" or "事件" => "事件",
            "group" or "组对象" => "组对象",
            _ => type
        };
    }

    private static IntPtr FindRealtimeDbEdit(IntPtr dialog, string role)
    {
        var dialogRect = UiAutomation.GetWindowRect(dialog);
        var edits = UiAutomation.EnumerateChildren(dialog)
            .Where(h => Native.GetClass(h).Equals("Edit", StringComparison.OrdinalIgnoreCase))
            .Select(h => new { Handle = h, Rect = UiAutomation.GetWindowRect(h) })
            .ToArray();

        var match = role switch
        {
            "name" => edits
                .Where(e => e.Rect.Width >= 110 && e.Rect.Width <= 150 && e.Rect.Left < dialogRect.Left + 260 && e.Rect.Top < dialogRect.Top + 140)
                .OrderBy(e => e.Rect.Top)
                .FirstOrDefault(),
            "initial" => edits
                .Where(e => e.Rect.Width >= 110 && e.Rect.Width <= 150 && e.Rect.Left < dialogRect.Left + 260 && e.Rect.Top >= dialogRect.Top + 120 && e.Rect.Top < dialogRect.Top + 170)
                .OrderBy(e => e.Rect.Top)
                .FirstOrDefault(),
            "unit" => edits
                .Where(e => e.Rect.Width >= 110 && e.Rect.Width <= 150 && e.Rect.Left < dialogRect.Left + 260 && e.Rect.Top >= dialogRect.Top + 145 && e.Rect.Top < dialogRect.Top + 200)
                .OrderBy(e => e.Rect.Top)
                .FirstOrDefault(),
            "note" => edits
                .Where(e => e.Rect.Width > 250 && e.Rect.Height <= 30 && e.Rect.Top > dialogRect.Top + 240)
                .OrderBy(e => e.Rect.Top)
                .FirstOrDefault(),
            _ => null
        };

        if (match == null) throw new InvalidOperationException("Realtime DB Edit not found: " + role);
        return match.Handle;
    }

    private static int WorkflowAddMomentaryButton(string[] args)
    {
        var label = Required(args, "--text");
        var variable = Required(args, "--variable");
        var windowIndex = ParseInt(args, "--window-index", 2);
        var x = ParseInt(args, "--x", 610);
        var y = ParseInt(args, "--y", 320);
        var width = ParseInt(args, "--width", 150);
        var height = ParseInt(args, "--height", 70);
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "momentary-button-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        var workflowProject = PrepareWorkflowProject(args, "window.button.add-momentary", outDir);
        var project = workflowProject.Project;

        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        var saved = false;
        var success = false;

        try
        {
            var beforeSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-before"));
            var beforeLabelCount = beforeSnapshot.CountBlobToken(label);
            if (beforeLabelCount > 0)
                throw new InvalidOperationException("Button label already exists in MCE blobs before workflow: " + label);

            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) return Fail("Failed to open editor.");
            main = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");

            UiAutomation.SendCommand(main, 33955);
            Thread.Sleep(700);

            var userList = FindListViewByItemCount(main, 3);
            UiAutomation.ListViewSelectIndex(userList, windowIndex);
            Thread.Sleep(250);
            if (!UiAutomation.ClickButtonByText(main, "动画组态", mouse: false))
                throw new InvalidOperationException("动画组态 button was not found.");
            Thread.Sleep(1000);

            var canvas = FindCanvas(main);
            UiAutomation.SendCommand(main, 32938);
            Thread.Sleep(300);
            UiAutomation.DragPoint(canvas, x, y, x + width, y + height, mouse: true);
            Thread.Sleep(600);
            UiAutomation.ClickPoint(canvas, x + width / 2, y + height / 2, MouseButton.Left, doubleClick: false, mouse: true);
            Thread.Sleep(300);

            UiAutomation.SendCommand(main, 32785);
            var dialog = UiAutomation.WaitForWindow(process.Id, "标准按钮构件属性设置", "#32770", TimeSpan.FromSeconds(8));
            if (dialog == IntPtr.Zero) throw new TimeoutException("标准按钮构件属性设置 dialog was not found.");
            Thread.Sleep(300);

            ConfigureStandardButtonText(dialog, label);

            var tab = FindFirstChild(dialog, "SysTabControl32", null);
            UiAutomation.TabSelectIndex(tab, 1, mouse: true);
            Thread.Sleep(400);

            ConfigureMomentaryOperation(process.Id, dialog, "按下功能", "置1", variable, outDir);
            ConfigureMomentaryOperation(process.Id, dialog, "抬起功能", "清0", variable, outDir);

            CaptureProcessWindows(process.Id, Path.Combine(outDir, "configured-before-confirm"));
            if (!ClickButtonByNormalizedText(dialog, mouse: true, "检查(K)", "检查(&K)", "检查"))
                throw new InvalidOperationException("Button property check button was not found.");
            HandlePropertyConfirmDialogs(process.Id, dialog, TimeSpan.FromSeconds(8));
            Thread.Sleep(500);
            if (!ClickButtonByNormalizedText(dialog, mouse: true, "确认(Y)", "确认(&Y)", "确认"))
                throw new InvalidOperationException("Button property confirm button was not found.");
            HandlePropertyConfirmDialogs(process.Id, dialog, TimeSpan.FromSeconds(8));
            WaitForWindowClosed(dialog, TimeSpan.FromSeconds(8));
            Thread.Sleep(1000);

            UiAutomation.SendCommand(main, SaveCommandId);
            Thread.Sleep(2000);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "after-save"));
            var afterSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-after"));
            var tokenDeltas = BuildTokenDeltas(beforeSnapshot, afterSnapshot, new[] { label, variable });
            var labelDelta = tokenDeltas.First(delta => delta.Token == label);
            var variableDelta = tokenDeltas.First(delta => delta.Token == variable);
            var labelFound = labelDelta.Increased;
            var variableIncreased = variableDelta.Increased;
            var variableFound = variableDelta.PresentAfter;
            saved = true;
            CloseEditorProcess(process.Id, main, saveIntent: true);
            process = null;
            main = IntPtr.Zero;
            var propertyReadbackVerified = ReopenVerifyMomentaryButton(project, editor, label, variable, windowIndex,
                x, y, width, height, outDir, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)), afterSnapshot,
                out var reopenVerified);
            File.WriteAllText(Path.Combine(outDir, "result.json"),
                JsonSerializer.Serialize(new
                {
                    project,
                    label,
                    variable,
                    windowIndex,
                    rectangle = new { x, y, width, height },
                    tokenDeltas,
                    labelFound,
                    variableIncreased,
                    variableFound,
                    propertyReadbackVerified,
                    reopenVerified
                }, JsonOptions()),
                Encoding.UTF8);

            success = labelFound && variableFound && propertyReadbackVerified && reopenVerified;
            WriteMutatingWorkflowResult(workflowProject, outDir, new[]
            {
                labelFound ? RequiredPass("button-label-token", label) : RequiredFail("button-label-token", "Button label was not found in MCE evidence."),
                variableFound ? RequiredPass("button-variable-token", variable) : RequiredFail("button-variable-token", "Variable token was not found in MCE evidence."),
                propertyReadbackVerified
                    ? RequiredPass("momentary-press-release:" + variable, "press=set1, release=clear0")
                    : RequiredFail("momentary-press-release:" + variable, "Momentary press/release property readback failed."),
                reopenVerified ? RequiredPass("reopen-readback", label) : RequiredFail("reopen-readback", "Reopen readback failed.")
            }, extra: new Dictionary<string, object?>
            {
                ["createdUiObjects"] = new[]
                {
                    new
                    {
                        kind = "momentary-button",
                        text = label,
                        variable,
                        rect = new { x, y, width, height },
                        readback = propertyReadbackVerified && reopenVerified ? "PASS" : "UNKNOWN"
                    }
                },
                ["touchedDataObjects"] = new[] { variable },
                ["createdDataObjects"] = Array.Empty<string>(),
                ["modifiedDataObjects"] = new[] { variable },
                ["controlEvidence"] = new[]
                {
                    new
                    {
                        type = "momentary",
                        dataObject = variable,
                        press = "set1",
                        release = "clear0",
                        readback = propertyReadbackVerified ? "PASS" : "FAIL"
                    }
                }
            });
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.WriteLine("workflow evidence: " + outDir);
            Console.WriteLine(success
                ? "momentary button verification: PASS"
                : "momentary button verification: CHECK EVIDENCE");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            if (process != null)
            {
                try { CaptureProcessWindows(process.Id, Path.Combine(outDir, "failure")); } catch { }
            }
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.Error.WriteLine("workflow failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    var hwnd = main != IntPtr.Zero ? main : UiAutomation.FindMainWindow(process.Id);
                    foreach (var dialog in UiAutomation.TopWindowsForPid(process.Id).Where(h => h != hwnd && Native.GetClass(h) == "#32770"))
                    {
                        UiAutomation.CloseWindow(dialog);
                        Thread.Sleep(200);
                    }
                    if (hwnd != IntPtr.Zero)
                    {
                        if (saved) UiAutomation.SendCommand(hwnd, SaveCommandId);
                        Thread.Sleep(500);
                        UiAutomation.CloseWindow(hwnd);
                        UiAutomation.HandleCloseDialogs(process.Id, saved, TimeSpan.FromSeconds(12));
                    }
                }
                catch { }
            }
        }
    }

    private static int WorkflowDeviceChannelMap(string[] args)
    {
        var deviceText = Opt(args, "--device-text") ?? "Smart200";
        var area = (Opt(args, "--area") ?? "V").Trim().ToUpperInvariant();
        var channelType = Opt(args, "--channel-type") ?? DeviceAreaToChannelType(area);
        var channelTypeIndex = OptInt(args, "--channel-type-index") ?? DeviceAreaToChannelTypeIndex(area);
        var address = ParseInt(args, "--address", 603);
        var count = ParseInt(args, "--count", 4);
        var dataTypeIndex = ParseInt(args, "--data-type-index", 0);
        var dataTypeText = Opt(args, "--data-type");
        var access = Opt(args, "--access") ?? "读写";
        var connectBase = Opt(args, "--connect-base");
        var explicitExpectedChannels = Opts(args, "--expected-channel").ToArray();
        var overwriteConnections = Has(args, "--overwrite-connections");
        var timeout = TimeSpan.FromSeconds(ParseInt(args, "--timeout", 25));
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "device-channel-map-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        var workflowProject = PrepareWorkflowProject(args, "device.channel.map", outDir);
        var project = workflowProject.Project;

        if (address < 0) return Fail("--address must be >= 0.");
        if (count <= 0 || count > 256) return Fail("--count must be between 1 and 256.");
        var skipReopenVerify = Has(args, "--skip-reopen-verify");
        if (skipReopenVerify && !Has(args, "--debug-allow-skip-reopen-verify"))
            return Fail("--skip-reopen-verify is disabled for safety. Add --debug-allow-skip-reopen-verify only for profiling runs.");

        var expectedChannels = explicitExpectedChannels.Length > 0
            ? explicitExpectedChannels
            : BuildExpectedChannelTexts(access, area, address, count, dataTypeIndex);
        var expectedVariables = string.IsNullOrWhiteSpace(connectBase)
            ? Array.Empty<string>()
            : BuildExpectedQuickConnectVariables(connectBase, count);
        var allowExistingVariables = Has(args, "--allow-existing-variables");
        var allowExistingChannels = Has(args, "--allow-existing-channels");

        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        var saved = false;
        var success = false;

        try
        {
            var beforeSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-before"));
            var existingExpectedVariables = expectedVariables
                .Where(name => beforeSnapshot.FindDataObjects(name).Length > 0)
                .ToArray();
            if (existingExpectedVariables.Length > 0 && !allowExistingVariables)
            {
                throw new InvalidOperationException(
                    "Expected connected variables already exist before workflow. Use --allow-existing-variables only when reusing existing Data objects intentionally: " +
                    string.Join(", ", existingExpectedVariables));
            }

            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) return Fail("Failed to open editor.");
            main = WaitForMainWindow(process.Id, timeout);
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");

            CaptureProcessWindows(process.Id, Path.Combine(outDir, "00-opened"));

            var tree = EnterDeviceConfiguration(process.Id, main, deviceText, timeout);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "01-device-config"));

            var deviceDialog = OpenDeviceEditor(process.Id, tree, deviceText, timeout);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "02-device-editor"));

            var channelList = FindDeviceChannelList(deviceDialog);
            var beforeRows = ReadDeviceChannelRows(channelList);
            WriteDeviceRows(Path.Combine(outDir, "channels-before.json"), beforeRows);

            var existingTargets = expectedChannels.Length == 0
                ? Array.Empty<DeviceChannelRow>()
                : FindDeviceChannelRows(beforeRows, expectedChannels);
            var conflictingExistingTargets = expectedChannels
                .Select((channel, index) => new
                {
                    channel,
                    expectedVariable = index < expectedVariables.Length ? expectedVariables[index] : "",
                    rows = beforeRows.Where(row => row.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)).ToArray()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.expectedVariable) &&
                               item.rows.Any(row => !string.IsNullOrWhiteSpace(row.Variable) &&
                                                    !row.Variable.Equals(item.expectedVariable, StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.channel + " -> expected " + item.expectedVariable)
                .ToArray();
            if (conflictingExistingTargets.Length > 0)
            {
                throw new InvalidOperationException("Target PLC address is already mapped to a different variable: " +
                                                    string.Join(", ", conflictingExistingTargets));
            }
            if (existingTargets.Length > 0 && !allowExistingChannels)
            {
                throw new InvalidOperationException(
                    "Target channels already exist before workflow. Use --allow-existing-channels only when intentionally reusing existing rows: " +
                    string.Join(", ", existingTargets.Select(row => row.Channel)));
            }
            if (expectedChannels.Length > 0 && existingTargets.Length > 0 && existingTargets.Length < expectedChannels.Length)
            {
                throw new InvalidOperationException(
                    "Only some target channels already exist. Refusing to add a partial duplicate set: " +
                    string.Join(", ", expectedChannels));
            }

            DeviceChannelRow[] targetRows;
            if (expectedChannels.Length > 0 && existingTargets.Length == expectedChannels.Length)
            {
                targetRows = existingTargets;
            }
            else
            {
                AddDeviceChannels(process.Id, deviceDialog, channelType, channelTypeIndex, dataTypeText, dataTypeIndex, address, count, access, outDir);
                targetRows = expectedChannels.Length > 0
                    ? WaitForDeviceChannels(deviceDialog, expectedChannels, TimeSpan.FromSeconds(8), out channelList)
                    : WaitForNewDeviceChannels(deviceDialog, beforeRows, count, TimeSpan.FromSeconds(8), out channelList);
                if (expectedChannels.Length == 0)
                    expectedChannels = targetRows.Select(row => row.Channel).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            }

            var rowsAfterAdd = ReadDeviceChannelRows(channelList);
            WriteDeviceRows(Path.Combine(outDir, "channels-after-add.json"), rowsAfterAdd);
            var channelRowDelta = rowsAfterAdd.Length - beforeRows.Length;
            var channelDeltaVerified = allowExistingChannels
                ? existingTargets.Length == expectedChannels.Length || channelRowDelta == count
                : channelRowDelta == count;

            if (!string.IsNullOrWhiteSpace(connectBase))
            {
                QuickConnectDeviceVariables(process.Id, deviceDialog, channelList, targetRows[0], connectBase, count,
                    overwriteConnections, outDir);
                targetRows = WaitForDeviceChannels(deviceDialog, expectedChannels, TimeSpan.FromSeconds(8), out channelList);
                WriteDeviceRows(Path.Combine(outDir, "channels-after-connect.json"), ReadDeviceChannelRows(channelList));
            }

            CaptureProcessWindows(process.Id, Path.Combine(outDir, "03-before-device-confirm"));
            if (!ClickButtonByNormalizedText(deviceDialog, mouse: true, "确认", "确       认"))
                throw new InvalidOperationException("Device editor confirm button was not found.");

            HandleDeviceConfirmDialogs(process.Id, deviceDialog, TimeSpan.FromSeconds(12));
            WaitForWindowClosed(deviceDialog, TimeSpan.FromSeconds(12));

            UiAutomation.SendCommand(main, SaveCommandId);
            Thread.Sleep(2000);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "04-after-save"));
            var afterSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-after"));

            var channelStringsFound = expectedChannels.All(s => afterSnapshot.CountBlobToken(s) > 0);
            var variableStringsFound = expectedVariables.Length == 0 ||
                                       expectedVariables.All(s => afterSnapshot.CountBlobToken(s) > 0 ||
                                                                  afterSnapshot.FindDataObjects(s).Length > 0);
            var expectedVariableDeltas = expectedVariables
                .Select(name =>
                {
                    var beforeCount = beforeSnapshot.FindDataObjects(name).Length;
                    var afterCount = afterSnapshot.FindDataObjects(name).Length;
                    return new
                    {
                        name,
                        beforeCount,
                        afterCount,
                        delta = afterCount - beforeCount
                    };
                })
                .ToArray();
            var dataObjectsDeltaVerified = expectedVariables.Length == 0 ||
                                           expectedVariableDeltas.All(delta =>
                                               allowExistingVariables
                                                   ? delta.afterCount > 0
                                                   : delta.beforeCount == 0 && delta.afterCount == 1 && delta.delta == 1);
            var guiRowsVerified = expectedChannels.All(expected =>
                targetRows.Any(row => row.Texts.Any(t => t.Equals(expected, StringComparison.OrdinalIgnoreCase))));
            var guiVariablesVerified = expectedVariables.Length == 0 ||
                                       expectedVariables.All(expected =>
                                           targetRows.Any(row => row.Texts.Any(t => t.Equals(expected, StringComparison.OrdinalIgnoreCase))));

            saved = true;
            var reopenVerified = true;
            DeviceChannelRow[] reopenRows = Array.Empty<DeviceChannelRow>();
            if (!skipReopenVerify)
            {
                CloseEditorProcess(process.Id, main, saveIntent: true);
                process = null;
                main = IntPtr.Zero;
                reopenVerified = ReopenVerifyDeviceChannelMap(project, editor, deviceText, expectedChannels,
                    expectedVariables, outDir, timeout, out reopenRows);
            }
            else
            {
                reopenRows = targetRows;
            }
            var smart200Channels = BuildSmart200ChannelEvidence(
                reopenRows.Length > 0 ? reopenRows : targetRows,
                expectedChannels,
                expectedVariables);

            File.WriteAllText(Path.Combine(outDir, "result.json"),
                JsonSerializer.Serialize(new
                {
                    project,
                    deviceText,
                    area,
                    channelType,
                    channelTypeIndex,
                    address,
                    count,
                    dataTypeIndex,
                    dataTypeText,
                    access,
                    connectBase,
                    overwriteConnections,
                    expectedChannels,
                    expectedVariables,
                    channelStringsFound,
                    variableStringsFound,
                    allowExistingVariables,
                    allowExistingChannels,
                    existingExpectedVariables,
                    existingTargetChannels = existingTargets.Select(row => row.Channel).ToArray(),
                    channelRowDelta,
                    channelDeltaVerified,
                    expectedVariableDeltas,
                    dataObjectsDeltaVerified,
                    guiRowsVerified,
                    guiVariablesVerified,
                    reopenVerified,
                    smart200Channels
                }, JsonOptions()),
                Encoding.UTF8);

            Console.WriteLine("workflow evidence: " + outDir);
            var passed = guiRowsVerified && guiVariablesVerified && variableStringsFound && dataObjectsDeltaVerified &&
                         channelDeltaVerified && reopenVerified;
            success = passed;
            WriteMutatingWorkflowResult(workflowProject, outDir, new[]
            {
                channelDeltaVerified ? RequiredPass("smart200-channel-row-delta") : RequiredFail("smart200-channel-row-delta", "Smart200 channel row delta did not match expected count."),
                guiRowsVerified ? RequiredPass("smart200-channel-rows") : RequiredFail("smart200-channel-rows", "Expected Smart200 rows were not visible in GUI table."),
                dataObjectsDeltaVerified ? RequiredPass("quick-connect-data-objects") : RequiredFail("quick-connect-data-objects", "Quick-connect Data object delta did not match."),
                reopenVerified ? RequiredPass("reopen-readback") : RequiredFail("reopen-readback", "Reopened Smart200 channel table did not match expected rows.")
            }, extra: new Dictionary<string, object?>
            {
                ["smart200Channels"] = smart200Channels,
                ["touchedDataObjects"] = expectedVariables,
                ["createdDataObjects"] = expectedVariables,
                ["modifiedDataObjects"] = Array.Empty<string>(),
                ["controlEvidence"] = Array.Empty<object>()
            });
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.WriteLine(passed
                ? "device channel map verification: PASS"
                : "device channel map verification: CHECK EVIDENCE");
            return passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            if (process != null)
            {
                try { CaptureProcessWindows(process.Id, Path.Combine(outDir, "failure")); } catch { }
            }
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.Error.WriteLine("workflow failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    var hwnd = main != IntPtr.Zero ? main : UiAutomation.FindMainWindow(process.Id);
                    foreach (var dialog in UiAutomation.TopWindowsForPid(process.Id).Where(h => h != hwnd && Native.GetClass(h) == "#32770"))
                    {
                        UiAutomation.CloseWindow(dialog);
                        Thread.Sleep(200);
                    }
                    if (hwnd != IntPtr.Zero)
                    {
                        if (saved) UiAutomation.SendCommand(hwnd, SaveCommandId);
                        Thread.Sleep(500);
                        UiAutomation.CloseWindow(hwnd);
                        UiAutomation.HandleCloseDialogs(process.Id, saved, TimeSpan.FromSeconds(12));
                    }
                }
                catch { }
            }
        }
    }

    private static int WorkflowWindowIndicatorAdd(string[] args)
    {
        var label = Required(args, "--text");
        var expression = Required(args, "--expression");
        var windowIndex = ParseInt(args, "--window-index", 2);
        var x = ParseInt(args, "--x", 610);
        var y = ParseInt(args, "--y", 320);
        var width = ParseInt(args, "--width", 150);
        var height = ParseInt(args, "--height", 50);
        var invisibleWhenNonzero = Has(args, "--invisible-when-nonzero");
        var syntheticLabel = Has(args, "--synthetic-label");
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "indicator-add-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        var workflowProject = PrepareWorkflowProject(args, "window.indicator.add", outDir);
        var project = workflowProject.Project;

        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        var saved = false;
        var success = false;

        try
        {
            var beforeSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-before"));
            var beforeLabelCount = beforeSnapshot.CountBlobToken(label);
            if (beforeLabelCount > 0)
                throw new InvalidOperationException("Indicator label already exists in MCE blobs before workflow: " + label);

            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) return Fail("Failed to open editor.");
            main = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");

            UiAutomation.SendCommand(main, 33955);
            Thread.Sleep(700);

            var userList = FindListViewByItemCount(main, 3);
            UiAutomation.ListViewSelectIndex(userList, windowIndex);
            Thread.Sleep(250);
            if (!ClickButtonByNormalizedText(main, mouse: true, "动画组态"))
                throw new InvalidOperationException("动画组态 button was not found.");
            Thread.Sleep(1000);

            var canvas = FindCanvas(main);
            UiAutomation.SendCommand(main, 32938);
            Thread.Sleep(300);
            UiAutomation.DragPoint(canvas, x, y, x + width, y + height, mouse: true);
            Thread.Sleep(600);
            UiAutomation.ClickPoint(canvas, x + width / 2, y + height / 2, MouseButton.Left, doubleClick: false, mouse: true);
            Thread.Sleep(300);

            UiAutomation.SendCommand(main, 32785);
            var dialog = UiAutomation.WaitForWindow(process.Id, "标准按钮构件属性设置", "#32770", TimeSpan.FromSeconds(8));
            if (dialog == IntPtr.Zero) throw new TimeoutException("标准按钮构件属性设置 dialog was not found.");
            Thread.Sleep(300);

            ConfigureStandardButtonText(dialog, label);

            var tab = FindFirstChild(dialog, "SysTabControl32", null);
            UiAutomation.TabSelectIndex(tab, 3, mouse: true);
            Thread.Sleep(400);
            var expressionEdit = FindVisibilityExpressionEdit(dialog);
            UiAutomation.SetControlText(expressionEdit, expression, paste: true);
            Thread.Sleep(250);
            if (invisibleWhenNonzero)
            {
                ClickButtonByNormalizedText(dialog, mouse: true, "按钮不可见");
                Thread.Sleep(150);
            }
            else
            {
                ClickButtonByNormalizedText(dialog, mouse: true, "按钮可见");
                Thread.Sleep(150);
            }

            CaptureProcessWindows(process.Id, Path.Combine(outDir, "configured-before-confirm"));
            if (!ClickButtonByNormalizedText(dialog, mouse: true, "确认(Y)", "确认(&Y)", "确认"))
                throw new InvalidOperationException("Button property confirm button was not found.");
            HandlePropertyConfirmDialogs(process.Id, dialog, TimeSpan.FromSeconds(8));
            WaitForWindowClosed(dialog, TimeSpan.FromSeconds(8));
            Thread.Sleep(1000);

            UiAutomation.SendCommand(main, SaveCommandId);
            Thread.Sleep(2000);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "after-save"));
            var afterSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-after"));
            var tokenDeltas = BuildTokenDeltas(beforeSnapshot, afterSnapshot, new[] { label, expression });
            var labelDelta = tokenDeltas.First(delta => delta.Token == label);
            var expressionDelta = tokenDeltas.First(delta => delta.Token == expression);
            var labelFound = labelDelta.Increased;
            var expressionIncreased = expressionDelta.Increased;
            var expressionFound = expressionDelta.PresentAfter;
            saved = true;
            CloseEditorProcess(process.Id, main, saveIntent: true);
            process = null;
            main = IntPtr.Zero;
            var reopenSnapshot = ReopenProjectAndExportSnapshot(project, editor, outDir,
                "reopen-verify", "mce-reopen", TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            var reopenTokens = syntheticLabel ? new[] { label } : new[] { label, expression };
            var reopenVerified = ReopenTokenCountsPreserved(afterSnapshot, reopenSnapshot, reopenTokens);
            var indicatorReadbackVerified = ReopenVerifyStatusButtonIndicator(project, editor, label, expression,
                windowIndex, x, y, width, height, outDir, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            File.WriteAllText(Path.Combine(outDir, "result.json"),
                JsonSerializer.Serialize(new
                {
                    project,
                    label,
                    expression,
                    syntheticLabel,
                    invisibleWhenNonzero,
                    windowIndex,
                    rectangle = new { x, y, width, height },
                    tokenDeltas,
                    reopenTokens,
                    labelFound,
                    expressionIncreased,
                    expressionFound,
                    reopenVerified,
                    indicatorReadbackVerified
                }, JsonOptions()),
                Encoding.UTF8);

            success = labelFound && expressionFound && reopenVerified && indicatorReadbackVerified;
            WriteMutatingWorkflowResult(workflowProject, outDir, new[]
            {
                labelFound ? RequiredPass("status-button-label-token", label) : RequiredFail("status-button-label-token", "Status-button label was not found."),
                expressionFound ? RequiredPass("status-button-expression-token", expression) : RequiredFail("status-button-expression-token", "Visibility expression token was not found."),
                reopenVerified ? RequiredPass("reopen-token-preserved") : RequiredFail("reopen-token-preserved", "Reopen token evidence did not match."),
                indicatorReadbackVerified
                    ? RequiredPass("status-button-property-readback", "label, visibility expression, no operation, and empty script verified")
                    : RequiredUnknown("status-button-property-readback", "Status-button property readback failed or was incomplete.")
            }, indicatorReadbackVerified
                ? syntheticLabel
                    ? new[] { "synthesized label is rendered as a status-button with constant visibility, not native static text" }
                    : new[] { "indicator is a status-button, not a native lamp" }
                : new[] { "status-button property readback incomplete; native lamp is not implemented" },
                new Dictionary<string, object?>
                {
                    ["createdUiObjects"] = new[]
                    {
                        new
                        {
                            kind = "status-button",
                            text = label,
                            expression,
                            rect = new { x, y, width, height },
                            readback = indicatorReadbackVerified && reopenVerified ? "PASS" : "UNKNOWN"
                        }
                    },
                    ["touchedDataObjects"] = Array.Empty<string>(),
                    ["createdDataObjects"] = Array.Empty<string>(),
                    ["modifiedDataObjects"] = Array.Empty<string>(),
                    ["controlEvidence"] = Array.Empty<object>()
                });
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.WriteLine("workflow evidence: " + outDir);
            Console.WriteLine(success
                ? "indicator add verification: PASS"
                : "indicator add verification: CHECK EVIDENCE");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            if (process != null)
            {
                try { CaptureProcessWindows(process.Id, Path.Combine(outDir, "failure")); } catch { }
            }
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.Error.WriteLine("workflow failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saved); } catch { }
            }
        }
    }

    private static int WorkflowNativeStaticTextAdd(string[] args)
    {
        var label = Required(args, "--text");
        var windowIndex = ParseInt(args, "--window-index", 2);
        var x = ParseInt(args, "--x", 610);
        var y = ParseInt(args, "--y", 320);
        var width = ParseInt(args, "--width", 180);
        var height = ParseInt(args, "--height", 40);
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "native-static-text-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        var workflowProject = PrepareWorkflowProject(args, "window.static-text.add", outDir);
        var project = workflowProject.Project;

        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        var saved = false;
        var success = false;

        try
        {
            var beforeSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-before"));
            var beforeLabelCount = beforeSnapshot.CountBlobToken(label);
            if (beforeLabelCount > 0)
                throw new InvalidOperationException("Native static text label already exists in MCE blobs before workflow: " + label);

            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) return Fail("Failed to open editor.");
            main = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");

            OpenAnimationConfiguration(process.Id, main, windowIndex, "native static text");
            var canvas = FindCanvas(main);
            UiAutomation.SendCommand(main, NativeStaticTextCommandId);
            Thread.Sleep(300);
            UiAutomation.DragPoint(canvas, x, y, x + width, y + height, mouse: true);
            Thread.Sleep(600);
            var dialog = OpenCanvasObjectPropertyDialog(process.Id, main, canvas, x, y, width, height,
                NativeStaticTextDialogTitle, preferCommand: true);
            ConfigureNativeStaticText(dialog, label);

            CaptureProcessWindows(process.Id, Path.Combine(outDir, "configured-before-confirm"));
            if (!ClickButtonByNormalizedText(dialog, mouse: true, "\u68c0\u67e5(&K)", "\u68c0\u67e5"))
                throw new InvalidOperationException("Native static text property check button was not found.");
            HandlePropertyConfirmDialogs(process.Id, dialog, TimeSpan.FromSeconds(8));
            Thread.Sleep(300);
            if (!ClickButtonByNormalizedText(dialog, mouse: true, "\u786e\u8ba4(&Y)", "\u786e\u8ba4"))
                throw new InvalidOperationException("Native static text property confirm button was not found.");
            HandlePropertyConfirmDialogs(process.Id, dialog, TimeSpan.FromSeconds(8));
            WaitForWindowClosed(dialog, TimeSpan.FromSeconds(8));
            Thread.Sleep(1000);

            UiAutomation.SendCommand(main, SaveCommandId);
            Thread.Sleep(2000);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "after-save"));
            var afterSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-after"));
            var tokenDeltas = BuildTokenDeltas(beforeSnapshot, afterSnapshot, new[] { label });
            var labelDelta = tokenDeltas.First(delta => delta.Token == label);
            var labelFound = labelDelta.Increased;
            saved = true;
            CloseEditorProcess(process.Id, main, saveIntent: true);
            process = null;
            main = IntPtr.Zero;

            var reopenSnapshot = ReopenProjectAndExportSnapshot(project, editor, outDir,
                "reopen-verify", "mce-reopen", TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            var reopenVerified = ReopenTokenCountsPreserved(afterSnapshot, reopenSnapshot, new[] { label });
            var propertyReadbackVerified = ReopenVerifyNativeStaticText(project, editor, label, windowIndex,
                x, y, width, height, outDir, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));

            File.WriteAllText(Path.Combine(outDir, "result.json"),
                JsonSerializer.Serialize(new
                {
                    project,
                    label,
                    commandId = NativeStaticTextCommandId,
                    windowIndex,
                    rectangle = new { x, y, width, height },
                    tokenDeltas,
                    labelFound,
                    reopenVerified,
                    propertyReadbackVerified
                }, JsonOptions()), Encoding.UTF8);

            success = labelFound && reopenVerified && propertyReadbackVerified;
            WriteMutatingWorkflowResult(workflowProject, outDir, new[]
            {
                labelFound ? RequiredPass("native-static-text-token", label) : RequiredFail("native-static-text-token", "Native static text label was not found."),
                reopenVerified ? RequiredPass("reopen-token-preserved") : RequiredFail("reopen-token-preserved", "Reopen token evidence did not match."),
                propertyReadbackVerified
                    ? RequiredPass("native-static-text-property-readback", "label text verified in native label property dialog")
                    : RequiredUnknown("native-static-text-property-readback", "Native static text property readback failed or was incomplete.")
            }, Array.Empty<string>(),
                new Dictionary<string, object?>
                {
                    ["createdUiObjects"] = new[]
                    {
                        new { kind = "native-static-text", text = label, rect = new { x, y, width, height }, readback = propertyReadbackVerified ? "PASS" : "UNKNOWN" }
                    },
                    ["touchedDataObjects"] = Array.Empty<string>(),
                    ["createdDataObjects"] = Array.Empty<string>(),
                    ["modifiedDataObjects"] = Array.Empty<string>(),
                    ["controlEvidence"] = Array.Empty<object>()
                });
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.WriteLine("workflow evidence: " + outDir);
            Console.WriteLine(success ? "native static text verification: PASS" : "native static text verification: CHECK EVIDENCE");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            if (process != null)
            {
                try { CaptureProcessWindows(process.Id, Path.Combine(outDir, "failure")); } catch { }
            }
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.Error.WriteLine("workflow failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saved); }
                catch { }
            }
        }
    }

    private static int WorkflowNativeLampAdd(string[] args)
    {
        var label = Required(args, "--text");
        var expression = Required(args, "--expression");
        var windowIndex = ParseInt(args, "--window-index", 2);
        var x = ParseInt(args, "--x", 610);
        var y = ParseInt(args, "--y", 320);
        var width = ParseInt(args, "--width", 120);
        var height = ParseInt(args, "--height", 75);
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "native-lamp-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        var workflowProject = PrepareWorkflowProject(args, "window.lamp.add-native", outDir);
        var project = workflowProject.Project;

        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        var saved = false;
        var success = false;

        try
        {
            var beforeSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-before"));
            var beforeLabelCount = beforeSnapshot.CountBlobToken(label);
            if (beforeLabelCount > 0)
                throw new InvalidOperationException("Native lamp label already exists in MCE blobs before workflow: " + label);

            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) return Fail("Failed to open editor.");
            main = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");

            OpenAnimationConfiguration(process.Id, main, windowIndex, "native lamp");
            var canvas = FindCanvas(main);
            UiAutomation.SendCommand(main, NativeLampCommandId);
            Thread.Sleep(300);
            UiAutomation.DragPoint(canvas, x, y, x + width, y + height, mouse: true);
            Thread.Sleep(600);
            var dialog = OpenCanvasObjectPropertyDialog(process.Id, main, canvas, x, y, width, height,
                NativeLampDialogTitle, preferCommand: false);
            ConfigureNativeLamp(dialog, label, expression);

            CaptureProcessWindows(process.Id, Path.Combine(outDir, "configured-before-confirm"));
            if (!ClickButtonByNormalizedText(dialog, mouse: true, "\u68c0\u67e5(&K)", "\u68c0\u67e5"))
                throw new InvalidOperationException("Native lamp property check button was not found.");
            HandlePropertyConfirmDialogs(process.Id, dialog, TimeSpan.FromSeconds(8));
            Thread.Sleep(300);
            if (!ClickButtonByNormalizedText(dialog, mouse: true, "\u786e\u8ba4(&Y)", "\u786e\u8ba4"))
                throw new InvalidOperationException("Native lamp property confirm button was not found.");
            HandlePropertyConfirmDialogs(process.Id, dialog, TimeSpan.FromSeconds(8));
            WaitForWindowClosed(dialog, TimeSpan.FromSeconds(8));
            Thread.Sleep(1000);

            UiAutomation.SendCommand(main, SaveCommandId);
            Thread.Sleep(2000);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "after-save"));
            var afterSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-after"));
            var tokenDeltas = BuildTokenDeltas(beforeSnapshot, afterSnapshot, new[] { label, expression });
            var labelDelta = tokenDeltas.First(delta => delta.Token == label);
            var expressionDelta = tokenDeltas.First(delta => delta.Token == expression);
            var labelFound = labelDelta.Increased;
            var expressionFound = expressionDelta.PresentAfter;
            saved = true;
            CloseEditorProcess(process.Id, main, saveIntent: true);
            process = null;
            main = IntPtr.Zero;

            var reopenSnapshot = ReopenProjectAndExportSnapshot(project, editor, outDir,
                "reopen-verify", "mce-reopen", TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            var reopenVerified = ReopenTokenCountsPreserved(afterSnapshot, reopenSnapshot, new[] { label, expression });
            var propertyReadbackVerified = ReopenVerifyNativeLamp(project, editor, label, expression, windowIndex,
                x, y, width, height, outDir, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));

            File.WriteAllText(Path.Combine(outDir, "result.json"),
                JsonSerializer.Serialize(new
                {
                    project,
                    label,
                    expression,
                    commandId = NativeLampCommandId,
                    windowIndex,
                    rectangle = new { x, y, width, height },
                    tokenDeltas,
                    labelFound,
                    expressionFound,
                    reopenVerified,
                    propertyReadbackVerified
                }, JsonOptions()), Encoding.UTF8);

            success = labelFound && expressionFound && reopenVerified && propertyReadbackVerified;
            WriteMutatingWorkflowResult(workflowProject, outDir, new[]
            {
                labelFound ? RequiredPass("native-lamp-label-token", label) : RequiredFail("native-lamp-label-token", "Native lamp label was not found."),
                expressionFound ? RequiredPass("native-lamp-expression-token", expression) : RequiredFail("native-lamp-expression-token", "Native lamp display variable was not found."),
                reopenVerified ? RequiredPass("reopen-token-preserved") : RequiredFail("reopen-token-preserved", "Reopen token evidence did not match."),
                propertyReadbackVerified
                    ? RequiredPass("native-lamp-property-readback", "display text and display variable verified in native animation display property dialog")
                    : RequiredUnknown("native-lamp-property-readback", "Native lamp property readback failed or was incomplete.")
            }, new[] { "native lamp is implemented as MCGS animation display component; hardware indication still requires human/site acceptance" },
                new Dictionary<string, object?>
                {
                    ["createdUiObjects"] = new[]
                    {
                        new { kind = "native-lamp", text = label, expression, rect = new { x, y, width, height }, readback = propertyReadbackVerified ? "PASS" : "UNKNOWN" }
                    },
                    ["touchedDataObjects"] = Array.Empty<string>(),
                    ["createdDataObjects"] = Array.Empty<string>(),
                    ["modifiedDataObjects"] = Array.Empty<string>(),
                    ["controlEvidence"] = Array.Empty<object>()
                });
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.WriteLine("workflow evidence: " + outDir);
            Console.WriteLine(success ? "native lamp verification: PASS" : "native lamp verification: CHECK EVIDENCE");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            if (process != null)
            {
                try { CaptureProcessWindows(process.Id, Path.Combine(outDir, "failure")); } catch { }
            }
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.Error.WriteLine("workflow failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saved); }
                catch { }
            }
        }
    }

    private static int WorkflowScriptEdit(string[] args)
    {
        var script = Opt(args, "--text");
        var file = Opt(args, "--file");
        if (script == null && file == null) return Fail("script.edit requires --text or --file.");
        if (file != null) script = File.ReadAllText(FullPath(file), Encoding.UTF8);
        script ??= "";

        var eventName = (Opt(args, "--event") ?? "down").Trim().ToLowerInvariant();
        var eventButton = eventName switch
        {
            "down" or "press" or "pressed" => "按下脚本",
            "up" or "release" or "released" => "抬起脚本",
            _ => throw new ArgumentException("--event must be down or up.")
        };
        var label = Opt(args, "--button-text") ?? "MCGSCTL_SCRIPT";
        var windowIndex = ParseInt(args, "--window-index", 2);
        var x = ParseInt(args, "--x", 800);
        var y = ParseInt(args, "--y", 320);
        var width = ParseInt(args, "--width", 160);
        var height = ParseInt(args, "--height", 70);
        var checkScript = Has(args, "--check");
        var allowCreateDataObjects = Has(args, "--allow-create-dataobjects");
        var expectedNewDataObjects = ParseNameList(args, "--expected-new-dataobjects");
        if (allowCreateDataObjects && expectedNewDataObjects.Length == 0)
            return Fail("--allow-create-dataobjects requires --expected-new-dataobjects <name[,name...]>.");
        if (!allowCreateDataObjects && expectedNewDataObjects.Length > 0)
            return Fail("--expected-new-dataobjects requires --allow-create-dataobjects.");
        var verifyTokens = Opts(args, "--verify-token").ToArray();
        if (verifyTokens.Length == 0)
        {
            verifyTokens = script
                .Replace("\r", "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => line.Length > 0)
                .Take(3)
                .ToArray();
        }
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "script-edit-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        var workflowProject = PrepareWorkflowProject(args, "script.edit", outDir);
        var project = workflowProject.Project;

        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        var saved = false;
        var success = false;

        try
        {
            var beforeSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-before"));
            var beforeLabelCount = beforeSnapshot.CountBlobToken(label);
            if (beforeLabelCount > 0)
                throw new InvalidOperationException("Script button label already exists in MCE blobs before workflow: " + label);

            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) return Fail("Failed to open editor.");
            main = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");

            UiAutomation.SendCommand(main, 33955);
            Thread.Sleep(700);

            var userList = FindListViewByItemCount(main, 3);
            UiAutomation.ListViewSelectIndex(userList, windowIndex);
            Thread.Sleep(250);
            if (!ClickButtonByNormalizedText(main, mouse: true, "动画组态"))
                throw new InvalidOperationException("动画组态 button was not found.");
            Thread.Sleep(1000);

            var canvas = FindCanvas(main);
            UiAutomation.SendCommand(main, 32938);
            Thread.Sleep(300);
            UiAutomation.DragPoint(canvas, x, y, x + width, y + height, mouse: true);
            Thread.Sleep(600);
            UiAutomation.ClickPoint(canvas, x + width / 2, y + height / 2, MouseButton.Left, doubleClick: false, mouse: true);
            Thread.Sleep(300);

            UiAutomation.SendCommand(main, 32785);
            var dialog = UiAutomation.WaitForWindow(process.Id, "标准按钮构件属性设置", "#32770", TimeSpan.FromSeconds(8));
            if (dialog == IntPtr.Zero) throw new TimeoutException("标准按钮构件属性设置 dialog was not found.");
            Thread.Sleep(300);

            ConfigureStandardButtonText(dialog, label);

            var tab = FindFirstChild(dialog, "SysTabControl32", null);
            UiAutomation.TabSelectIndex(tab, 2, mouse: true);
            Thread.Sleep(400);
            if (!ClickButtonByNormalizedText(dialog, mouse: true, eventButton))
                throw new InvalidOperationException(eventButton + " button was not found.");
            Thread.Sleep(200);
            if (!ClickButtonByNormalizedText(dialog, mouse: true, "打开脚本程序编辑器"))
                throw new InvalidOperationException("打开脚本程序编辑器 button was not found.");

            var scriptDialog = WaitForTopWindow(process.Id,
                h => Native.GetClass(h) == "#32770" &&
                     CompactLabel(Native.GetText(h)).Contains(CompactLabel("脚本程序"), StringComparison.OrdinalIgnoreCase) &&
                     h != dialog,
                TimeSpan.FromSeconds(8));
            if (scriptDialog == IntPtr.Zero) throw new TimeoutException("脚本程序 dialog was not found.");
            Thread.Sleep(300);

            var scriptEdit = FindLargestEdit(scriptDialog);
            UiAutomation.SetControlText(scriptEdit, script, paste: true);
            Thread.Sleep(400);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "script-editor-before-confirm"));

            if (checkScript)
            {
                if (!ClickButtonByNormalizedText(scriptDialog, mouse: true, "检查"))
                    throw new InvalidOperationException("Script check button was not found.");
                HandleScriptCheckDialogs(process.Id, scriptDialog, TimeSpan.FromSeconds(5));
                CaptureProcessWindows(process.Id, Path.Combine(outDir, "script-editor-after-check"));
            }

            ConfirmScriptDialog(process.Id, scriptDialog, dialog, allowCreateDataObjects);
            Thread.Sleep(500);

            var propertyDialog = Native.IsWindow(dialog) && Native.IsWindowVisible(dialog)
                ? dialog
                : WaitForTopWindow(process.Id,
                    h => Native.GetClass(h) == "#32770" &&
                         CompactLabel(Native.GetText(h)).Contains(CompactLabel("标准按钮构件属性设置"), StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(2));
            if (propertyDialog != IntPtr.Zero)
            {
                CaptureProcessWindows(process.Id, Path.Combine(outDir, "button-properties-before-confirm"));
                if (!ClickButtonByNormalizedText(propertyDialog, mouse: true, "确认(Y)", "确认(&Y)", "确认"))
                    throw new InvalidOperationException("Button property confirm button was not found.");
                WaitForWindowClosed(propertyDialog, TimeSpan.FromSeconds(8));
                Thread.Sleep(1000);
            }

            UiAutomation.SendCommand(main, SaveCommandId);
            Thread.Sleep(2000);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "after-save"));
            var afterSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-after"));
            var tokenDeltas = BuildTokenDeltas(beforeSnapshot, afterSnapshot, new[] { label }.Concat(verifyTokens));
            var labelDelta = tokenDeltas.First(delta => delta.Token == label);
            var labelFound = labelDelta.Increased;
            var scriptFound = verifyTokens.Length == 0 ||
                              verifyTokens.All(token => tokenDeltas.Any(delta =>
                                  delta.Token == token && delta.Increased));
            var beforeDataNames = beforeSnapshot.DataObjects
                .Select(row => row.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.Ordinal);
            var newDataObjects = afterSnapshot.DataObjects
                .Select(row => row.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name) && !beforeDataNames.Contains(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var expectedNewDataObjectsSorted = expectedNewDataObjects
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var newDataObjectsVerified = allowCreateDataObjects
                ? newDataObjects.SequenceEqual(expectedNewDataObjectsSorted, StringComparer.Ordinal)
                : newDataObjects.Length == 0;
            var touchedDataObjects = verifyTokens
                .Concat(newDataObjects)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var modifiedDataObjects = verifyTokens
                .Where(name => beforeDataNames.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            saved = true;
            CloseEditorProcess(process.Id, main, saveIntent: true);
            process = null;
            main = IntPtr.Zero;
            var reopenSnapshot = ReopenProjectAndExportSnapshot(project, editor, outDir,
                "reopen-verify", "mce-reopen", TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            var reopenVerified = ReopenTokenCountsPreserved(afterSnapshot, reopenSnapshot,
                new[] { label }.Concat(verifyTokens));
            File.WriteAllText(Path.Combine(outDir, "result.json"),
                JsonSerializer.Serialize(new
                {
                    project,
                    label,
                    eventName,
                    eventButton,
                    windowIndex,
                    rectangle = new { x, y, width, height },
                    checkScript,
                    allowCreateDataObjects,
                    expectedNewDataObjects,
                    newDataObjects,
                    newDataObjectsVerified,
                    verifyTokens,
                    tokenDeltas,
                    labelFound,
                    scriptFound,
                    reopenVerified,
                    touchedDataObjects,
                    createdDataObjects = newDataObjects,
                    modifiedDataObjects
                }, JsonOptions()),
                Encoding.UTF8);

            success = labelFound && scriptFound && newDataObjectsVerified && reopenVerified && checkScript;
            WriteMutatingWorkflowResult(workflowProject, outDir, new[]
            {
                checkScript ? RequiredPass("script-check-requested") : RequiredUnknown("script-check-requested", "script.edit requires --check for production candidates."),
                labelFound ? RequiredPass("script-button-label-token", label) : RequiredFail("script-button-label-token", "Script button label was not found."),
                scriptFound ? RequiredPass("script-token-delta") : RequiredFail("script-token-delta", "Expected script token was not found."),
                newDataObjectsVerified ? RequiredPass("script-new-dataobjects-delta") : RequiredFail("script-new-dataobjects-delta", "New Data object delta did not match expected whitelist."),
                reopenVerified ? RequiredPass("reopen-readback") : RequiredFail("reopen-readback", "Reopen token evidence did not match.")
            }, extra: new Dictionary<string, object?>
            {
                ["touchedDataObjects"] = touchedDataObjects,
                ["createdDataObjects"] = newDataObjects,
                ["modifiedDataObjects"] = modifiedDataObjects,
                ["controlEvidence"] = Array.Empty<object>()
            });
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.WriteLine("workflow evidence: " + outDir);
            Console.WriteLine(success
                ? "script edit verification: PASS"
                : "script edit verification: CHECK EVIDENCE");
            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            if (process != null)
            {
                try { CaptureProcessWindows(process.Id, Path.Combine(outDir, "failure")); } catch { }
            }
            WriteWorkflowAuditEnd(outDir, workflowProject, saved, success);
            Console.Error.WriteLine("workflow failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saved); } catch { }
            }
        }
    }

    private sealed record DeviceChannelRow(int ListIndex, int? ChannelNumber, string Variable, string Channel, string[] Texts);

    private sealed record AddChannelControls(IntPtr ChannelType, IntPtr DataType, IntPtr Address, IntPtr Count);

    private static IntPtr EnterDeviceConfiguration(int pid, IntPtr main, string deviceText, TimeSpan timeout)
    {
        UiAutomation.SendCommand(main, 33954);
        Thread.Sleep(800);

        var tree = WaitForTreeViewContaining(main, deviceText, TimeSpan.FromSeconds(2));
        if (tree != IntPtr.Zero) return tree;

        if (!ClickButtonByNormalizedText(main, mouse: true, "设备组态"))
            throw new InvalidOperationException("设备组态 button was not found.");
        Thread.Sleep(1000);

        tree = WaitForTreeViewContaining(main, deviceText, timeout);
        if (tree == IntPtr.Zero)
            throw new TimeoutException("Smart200 device tree was not found after entering device configuration.");
        return tree;
    }

    private static IntPtr OpenDeviceEditor(int pid, IntPtr tree, string deviceText, TimeSpan timeout)
    {
        var item = UiAutomation.TreeViewFindItem(tree, deviceText);
        var itemHandle = ParseHwnd(item.Handle);
        UiAutomation.TreeViewSelect(tree, itemHandle);
        Thread.Sleep(150);

        var rect = UiAutomation.TreeViewItemRect(tree, itemHandle);
        var x = Math.Max(80, rect.Left + Math.Min(120, Math.Max(30, rect.Width / 2)));
        var y = rect.Top + Math.Max(1, rect.Height / 2);
        UiAutomation.ClickPoint(tree, x, y, MouseButton.Left, doubleClick: true, mouse: true);

        var dialog = WaitForTopWindow(pid,
            h => Native.GetClass(h) == "#32770" &&
                 CompactLabel(Native.GetText(h)).Contains(CompactLabel("设备编辑窗口"), StringComparison.OrdinalIgnoreCase),
            timeout);
        if (dialog == IntPtr.Zero)
            throw new TimeoutException("设备编辑窗口 did not open from the Smart200 tree node.");
        return dialog;
    }

    private static void AddDeviceChannels(int pid, IntPtr deviceDialog, string channelType, int channelTypeIndex, string? dataTypeText,
        int dataTypeIndex, int address, int count, string access, string outDir)
    {
        if (!ClickButtonByNormalizedText(deviceDialog, mouse: true, "增加设备通道"))
            throw new InvalidOperationException("增加设备通道 button was not found.");

        var addDialog = WaitForTopWindow(pid,
            h => Native.GetClass(h) == "#32770" &&
                 CompactLabel(Native.GetText(h)).Contains(CompactLabel("添加设备通道"), StringComparison.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(8));
        if (addDialog == IntPtr.Zero) throw new TimeoutException("添加设备通道 dialog was not found.");
        Thread.Sleep(300);

        var controls = FindAddChannelControls(addDialog);
        if (channelTypeIndex >= 0)
            UiAutomation.ComboSelectIndex(controls.ChannelType, channelTypeIndex);
        else
            UiAutomation.ComboSelectText(controls.ChannelType, channelType);
        Thread.Sleep(150);
        if (string.IsNullOrWhiteSpace(dataTypeText))
            UiAutomation.ComboSelectIndex(controls.DataType, dataTypeIndex);
        else
            UiAutomation.ComboSelectText(controls.DataType, dataTypeText);
        Thread.Sleep(150);
        UiAutomation.SetControlText(controls.Address, address.ToString(), paste: true);
        UiAutomation.SetControlText(controls.Count, count.ToString(), paste: true);
        if (!ClickButtonByNormalizedText(addDialog, mouse: true, access))
            throw new InvalidOperationException("Access radio button was not found: " + access);

        CaptureProcessWindows(pid, Path.Combine(outDir, "add-channel-before-confirm"));
        if (!ClickButtonByNormalizedText(addDialog, mouse: true, "确认", "确       认"))
            throw new InvalidOperationException("Add-channel confirm button was not found.");
        WaitForWindowClosed(addDialog, TimeSpan.FromSeconds(8));
        Thread.Sleep(800);
    }

    private static void QuickConnectDeviceVariables(int pid, IntPtr deviceDialog, IntPtr channelList,
        DeviceChannelRow firstTargetRow, string connectBase, int count, bool overwriteConnections, string outDir)
    {
        UiAutomation.ListViewSelectIndex(channelList, firstTargetRow.ListIndex);
        Thread.Sleep(200);
        if (!ClickButtonByNormalizedText(deviceDialog, mouse: true, "快速连接变量"))
            throw new InvalidOperationException("快速连接变量 button was not found.");

        var quickDialog = WaitForTopWindow(pid,
            h => Native.GetClass(h) == "#32770" &&
                 CompactLabel(Native.GetText(h)).Contains(CompactLabel("快速连接"), StringComparison.OrdinalIgnoreCase),
            TimeSpan.FromSeconds(8));
        if (quickDialog == IntPtr.Zero) throw new TimeoutException("快速连接 dialog was not found.");
        Thread.Sleep(300);

        ClickButtonByNormalizedText(quickDialog, mouse: true, "自定义变量连接");
        var edits = OrderedControls(quickDialog, "Edit").ToArray();
        if (edits.Length < 3) throw new InvalidOperationException("快速连接 dialog did not expose three Edit controls.");

        var startChannel = firstTargetRow.ChannelNumber ?? firstTargetRow.ListIndex;
        UiAutomation.SetControlText(edits[0].Handle, connectBase, paste: true);
        UiAutomation.SetControlText(edits[1].Handle, startChannel.ToString(), paste: true);
        UiAutomation.SetControlText(edits[2].Handle, count.ToString(), paste: true);

        CaptureProcessWindows(pid, Path.Combine(outDir, "quick-connect-before-confirm"));
        if (!ClickButtonByNormalizedText(quickDialog, mouse: true, "确认", "确       认"))
            throw new InvalidOperationException("Quick-connect confirm button was not found.");

        if (!HandleQuickConnectDialogs(pid, overwriteConnections, TimeSpan.FromSeconds(8)))
            throw new InvalidOperationException("Quick-connect found existing channel-variable bindings. Rerun with --overwrite-connections if replacement is intended.");

        WaitForWindowClosed(quickDialog, TimeSpan.FromSeconds(8));
        Thread.Sleep(800);
    }

    private static AddChannelControls FindAddChannelControls(IntPtr dialog)
    {
        var combos = OrderedControls(dialog, "ComboBox")
            .OrderBy(c => c.Rect.Top)
            .Take(2)
            .OrderBy(c => c.Rect.Left)
            .ToArray();
        if (combos.Length < 2) throw new InvalidOperationException("Add-channel dialog did not expose two ComboBox controls.");
        var edits = OrderedControls(dialog, "Edit")
            .OrderBy(c => c.Rect.Top)
            .Take(2)
            .OrderBy(c => c.Rect.Left)
            .ToArray();
        if (edits.Length < 2) throw new InvalidOperationException("Add-channel dialog did not expose two Edit controls.");
        return new AddChannelControls(combos[0].Handle, combos[1].Handle, edits[0].Handle, edits[1].Handle);
    }

    private static void SelectComboIndexByKeyboard(IntPtr combo, int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        var rect = UiAutomation.GetWindowRect(combo);
        UiAutomation.ActivateForInput(combo);
        UiAutomation.ClickPoint(combo, Math.Max(1, rect.Width - 12), Math.Max(1, rect.Height / 2),
            MouseButton.Left, doubleClick: false, mouse: true);
        Thread.Sleep(250);

        var itemHeight = Math.Max(14, rect.Height - 5);
        var x = rect.Left + Math.Min(Math.Max(20, rect.Width / 3), Math.Max(20, rect.Width - 20));
        var y = rect.Bottom + itemHeight * index + itemHeight / 2;
        Native.SetCursorPos(x, y);
        Thread.Sleep(80);
        Native.MouseEvent(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        Native.MouseEvent(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(250);
    }

    private static (IntPtr Handle, Rect Rect, string Text)[] OrderedControls(IntPtr root, string className)
        => UiAutomation.EnumerateChildren(root)
            .Where(h => Native.GetClass(h).Equals(className, StringComparison.OrdinalIgnoreCase))
            .Select(h => (Handle: h, Rect: UiAutomation.GetWindowRect(h), Text: Native.GetText(h)))
            .OrderBy(c => c.Rect.Top)
            .ThenBy(c => c.Rect.Left)
            .ToArray();

    private static IntPtr WaitForTreeViewContaining(IntPtr root, string text, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var tree = UiAutomation.EnumerateChildren(root)
                .Where(h => Native.GetClass(h).Equals("SysTreeView32", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(h =>
                {
                    try { return UiAutomation.TreeViewItems(h).Any(i => i.Text.Contains(text, StringComparison.OrdinalIgnoreCase)); }
                    catch { return false; }
                });
            if (tree != IntPtr.Zero) return tree;
            Thread.Sleep(250);
        }
        return IntPtr.Zero;
    }

    private static IntPtr FindDeviceChannelList(IntPtr deviceDialog)
    {
        var list = UiAutomation.EnumerateChildren(deviceDialog)
            .Where(h => Native.GetClass(h).Equals("SysListView32", StringComparison.OrdinalIgnoreCase))
            .Select(h =>
            {
                try { return new { Handle = h, Count = UiAutomation.ListViewItems(h).Length }; }
                catch { return new { Handle = h, Count = -1 }; }
            })
            .Where(x => x.Count >= 0)
            .OrderByDescending(x => x.Count)
            .Select(x => x.Handle)
            .FirstOrDefault();
        if (list == IntPtr.Zero) throw new InvalidOperationException("Device channel ListView was not found.");
        return list;
    }

    private static DeviceChannelRow[] ReadDeviceChannelRows(IntPtr listView)
        => UiAutomation.ListViewItems(listView)
            .Select(item =>
            {
                var number = item.Texts.Length > 0 && int.TryParse(item.Texts[0], out var parsed) ? parsed : (int?)null;
                var variable = item.Texts.Length > 1 ? item.Texts[1] : "";
                var channel = item.Texts.Length > 2 ? item.Texts[2] : "";
                return new DeviceChannelRow(item.Index, number, variable, channel, item.Texts);
            })
            .ToArray();

    private static DeviceChannelRow[] FindDeviceChannelRows(DeviceChannelRow[] rows, string[] expectedChannels)
        => expectedChannels
            .Select(expected => rows.FirstOrDefault(row =>
                row.Texts.Any(t => t.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
                                    t.Contains(expected, StringComparison.OrdinalIgnoreCase))))
            .Where(row => row != null)
            .Cast<DeviceChannelRow>()
            .ToArray();

    private static DeviceChannelRow[] WaitForDeviceChannels(IntPtr deviceDialog, string[] expectedChannels,
        TimeSpan timeout, out IntPtr channelList)
    {
        var until = DateTime.UtcNow + timeout;
        channelList = IntPtr.Zero;
        while (DateTime.UtcNow < until)
        {
            channelList = FindDeviceChannelList(deviceDialog);
            var rows = ReadDeviceChannelRows(channelList);
            var matches = FindDeviceChannelRows(rows, expectedChannels);
            if (matches.Length == expectedChannels.Length) return matches;
            Thread.Sleep(300);
        }
        throw new TimeoutException("Expected device channels were not found: " + string.Join(", ", expectedChannels));
    }

    private static DeviceChannelRow[] WaitForNewDeviceChannels(IntPtr deviceDialog, DeviceChannelRow[] beforeRows,
        int expectedCount, TimeSpan timeout, out IntPtr channelList)
    {
        var before = beforeRows.Select(RowSignature).ToHashSet(StringComparer.Ordinal);
        var until = DateTime.UtcNow + timeout;
        channelList = IntPtr.Zero;
        while (DateTime.UtcNow < until)
        {
            channelList = FindDeviceChannelList(deviceDialog);
            var rows = ReadDeviceChannelRows(channelList);
            var added = rows
                .Where(row => !before.Contains(RowSignature(row)) && !string.IsNullOrWhiteSpace(row.Channel))
                .OrderBy(row => row.ListIndex)
                .Take(expectedCount)
                .ToArray();
            if (added.Length == expectedCount) return added;
            Thread.Sleep(300);
        }
        throw new TimeoutException("Expected newly added device channel rows were not found.");
    }

    private static string RowSignature(DeviceChannelRow row)
        => string.Join("\u001F", row.Texts);

    private static void WriteDeviceRows(string file, DeviceChannelRow[] rows)
    {
        File.WriteAllText(file, JsonSerializer.Serialize(rows, JsonOptions()), Encoding.UTF8);
    }

    private static object[] BuildSmart200ChannelEvidence(DeviceChannelRow[] rows, string[] expectedChannels,
        string[] expectedVariables)
    {
        var selected = rows
            .Where(row =>
                expectedChannels.Any(expected =>
                    row.Texts.Any(text => text.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
                                          text.Contains(expected, StringComparison.OrdinalIgnoreCase))) ||
                expectedVariables.Any(expected =>
                    row.Texts.Any(text => text.Equals(expected, StringComparison.OrdinalIgnoreCase))))
            .GroupBy(RowSignature)
            .Select(group => group.First())
            .OrderBy(row => row.ListIndex)
            .ToArray();

        return selected.Select(row =>
        {
            var channelText = row.Channel;
            return new
            {
                channelText,
                parsedAddress = ParseSmart200ChannelAddress(channelText),
                variable = row.Variable,
                access = ParseSmart200ChannelAccess(channelText),
                rowIndex = row.ListIndex
            };
        }).ToArray<object>();
    }

    private static string ParseSmart200ChannelAddress(string channelText)
    {
        var match = Regex.Match(channelText ?? "", @"([A-Za-z][A-Za-z0-9]*\d+(?:\.\d+)?)");
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : "";
    }

    private static string ParseSmart200ChannelAccess(string channelText)
    {
        var text = (channelText ?? "").Trim();
        foreach (var prefix in new[] { "读写", "只读", "只写", "读", "写" })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal)) return prefix;
        }
        return "";
    }

    private static string DeviceAreaToChannelType(string area)
        => area switch
        {
            "I" => "I输入继电器",
            "Q" => "Q输出继电器",
            "M" => "M内部继电器",
            "V" => "V数据寄存器",
            _ => throw new ArgumentException("Unknown --area. Use I, Q, M, or V.")
        };

    private static int DeviceAreaToChannelTypeIndex(string area)
        => area switch
        {
            "I" => 0,
            "Q" => 1,
            "M" => 2,
            "V" => 3,
            _ => -1
        };

    private static string[] BuildExpectedChannelTexts(string access, string area, int address, int count, int dataTypeIndex)
    {
        if (dataTypeIndex >= 0 && dataTypeIndex <= 7)
        {
            return Enumerable.Range(0, count)
                .Select(i => $"{access}{area}{address:000}.{dataTypeIndex + i}")
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static string[] BuildExpectedQuickConnectVariables(string connectBase, int count)
    {
        if (count <= 0) return Array.Empty<string>();
        if (count == 1) return new[] { connectBase };
        return Enumerable.Range(0, count)
            .Select(i => connectBase + i.ToString("00"))
            .ToArray();
    }

    private static bool ClickButtonByNormalizedText(IntPtr root, bool mouse, params string[] labels)
    {
        var targets = labels
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(CompactLabel)
            .Where(s => s.Length > 0)
            .ToArray();
        if (targets.Length == 0) return false;

        var buttons = UiAutomation.EnumerateChildren(root)
            .Where(h => Native.GetClass(h).Contains("Button", StringComparison.OrdinalIgnoreCase) && Native.IsWindowVisible(h))
            .Select(h => new { Handle = h, Text = Native.GetText(h), Compact = CompactLabel(Native.GetText(h)) })
            .ToArray();

        var match = targets
                        .Select(target => buttons.FirstOrDefault(b => b.Compact.Equals(target, StringComparison.OrdinalIgnoreCase)))
                        .FirstOrDefault(button => button != null)
                    ?? targets
                        .Select(target => buttons.FirstOrDefault(b => b.Compact.Contains(target, StringComparison.OrdinalIgnoreCase)))
                        .FirstOrDefault(button => button != null);
        if (match == null) return false;

        if (mouse)
        {
            var rect = UiAutomation.GetWindowRect(match.Handle);
            Native.SetForegroundWindow(Native.GetAncestor(match.Handle, Native.GA_ROOT));
            Native.SetCursorPos(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            Thread.Sleep(80);
            Native.MouseEvent(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            Native.MouseEvent(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }
        else
        {
            Native.SendMessage(match.Handle, Native.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }
        Thread.Sleep(250);
        return true;
    }

    private static string CompactLabel(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || ch == '&') continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static bool HandleQuickConnectDialogs(int pid, bool overwriteConnections, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        var ok = true;
        while (DateTime.UtcNow < until)
        {
            var handled = false;
            foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h => Native.GetClass(h) == "#32770"))
            {
                var title = Native.GetText(dialog);
                if (CompactLabel(title).Contains(CompactLabel("设备编辑窗口"), StringComparison.OrdinalIgnoreCase) ||
                    CompactLabel(title).Contains(CompactLabel("快速连接"), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = DialogText(dialog);
                if (ContainsAny(text, "已经连接", "重新连接", "清除原先"))
                {
                    ok = overwriteConnections;
                    if (overwriteConnections)
                    {
                        ClickButtonByNormalizedText(dialog, mouse: true, "全部清除", "清除", "确认", "确定");
                    }
                    else
                    {
                        ClickButtonByNormalizedText(dialog, mouse: true, "不清除", "取消", "否");
                    }
                    handled = true;
                    continue;
                }

                if (ContainsAny(text, "添加数据对象", "数据对象", "是否", "提示"))
                {
                    ClickButtonByNormalizedText(dialog, mouse: true, "全部添加", "添加", "是(Y)", "是", "确认", "确定");
                    handled = true;
                }
            }

            if (!handled) break;
            Thread.Sleep(500);
        }
        return ok;
    }

    private static void HandleStartupDialogs(int pid, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        var quietSince = DateTime.UtcNow;
        while (DateTime.UtcNow < until)
        {
            var handled = false;
            var dialogs = UiAutomation.TopWindowsForPid(pid)
                .Where(h => Native.GetClass(h) == "#32770")
                .ToArray();
            if (dialogs.Length == 0)
            {
                if (DateTime.UtcNow - quietSince >= TimeSpan.FromMilliseconds(1200)) break;
                Thread.Sleep(250);
                continue;
            }

            quietSince = DateTime.UtcNow;
            foreach (var dialog in dialogs)
            {
                var title = Native.GetText(dialog);
                var text = DialogText(dialog);
                if (ContainsAny(text, "存在同名的不同工程备份", "是否删除原备份"))
                {
                    RecordDialogEvidence("startup-dialogs.jsonl", pid, dialog, "click-yes", "startup.backup-prompt");
                    ClickButtonByNormalizedText(dialog, mouse: true, "是(Y)", "是");
                    handled = true;
                    continue;
                }

                if (title.Contains("选择数据源", StringComparison.OrdinalIgnoreCase))
                {
                    RecordDialogEvidence("startup-dialogs.jsonl", pid, dialog, "click-cancel", "startup.odbc-prompt");
                    ClickButtonByNormalizedText(dialog, mouse: true, "取消");
                    handled = true;
                    continue;
                }

                RecordDialogEvidence("startup-dialogs.jsonl", pid, dialog, "unexpected", "startup");
                throw new InvalidOperationException("Unexpected startup dialog: " + ShortDialogText(dialog));
            }

            if (!handled) break;
            Thread.Sleep(700);
            quietSince = DateTime.UtcNow;
        }
        RecordOpenPopups(pid, "startup");
    }

    private static void HandleDeviceConfirmDialogs(int pid, IntPtr deviceDialog, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var handled = false;
            var dialogs = UiAutomation.TopWindowsForPid(pid)
                .Where(h => Native.GetClass(h) == "#32770" && h != deviceDialog)
                .ToArray();
            foreach (var dialog in dialogs)
            {
                var text = DialogText(dialog);
                if (ContainsAny(text, "添加数据对象", "数据对象", "是否", "提示", "组态检查"))
                {
                    ClickButtonByNormalizedText(dialog, mouse: true, "全部添加", "添加", "是(Y)", "是", "确认", "确定");
                    handled = true;
                }
            }

            if (!handled &&
                (!Native.IsWindow(deviceDialog) || !Native.IsWindowVisible(deviceDialog)) &&
                dialogs.Length == 0)
            {
                return;
            }
            Thread.Sleep(500);
        }
    }

    private static string DialogText(IntPtr dialog)
        => Native.GetText(dialog) + "\n" + string.Join("\n", UiAutomation.EnumerateChildren(dialog).Select(Native.GetText));

    private static bool ContainsAny(string text, params string[] needles)
        => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static bool IsDialogErrorText(string text)
    {
        if (ContainsAny(text,
                "\u6ca1\u6709\u9519\u8bef",
                "\u65e0\u9519\u8bef",
                "0\u4e2a\u9519\u8bef",
                "0 \u4e2a\u9519\u8bef"))
        {
            return false;
        }

        return ContainsAny(text,
            "error", "Error", "ERROR",
            "\u9519\u8bef",
            "\u5931\u8d25",
            "\u4e0d\u901a\u8fc7");
    }

    private static bool IsUnknownObjectDialogText(string text)
        => ContainsAny(text,
            "\u672a\u77e5\u5bf9\u8c61",
            "\u662f\u5426\u589e\u52a0",
            "\u6dfb\u52a0\u6570\u636e\u5bf9\u8c61");

    private static bool IsBenignInfoDialogText(string text)
        => ContainsAny(text,
            "\u63d0\u793a",
            "\u6210\u529f",
            "\u5b8c\u6210",
            "\u6b63\u786e",
            "\u68c0\u67e5",
            "\u6ca1\u6709\u9519\u8bef",
            "\u65e0\u9519\u8bef");

    private static string ShortDialogText(IntPtr dialog)
    {
        var compact = Regex.Replace(DialogText(dialog), @"\s+", " ").Trim();
        return compact.Length <= 240 ? compact : compact[..240] + "...";
    }

    private static IntPtr WaitForTopWindow(int pid, Func<IntPtr, bool> predicate, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var hwnd = UiAutomation.TopWindowsForPid(pid).FirstOrDefault(predicate);
            if (hwnd != IntPtr.Zero) return hwnd;
            Thread.Sleep(150);
        }
        return IntPtr.Zero;
    }

    private static void WaitForWindowClosed(IntPtr hwnd, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (!Native.IsWindow(hwnd) || !Native.IsWindowVisible(hwnd)) return;
            Thread.Sleep(200);
        }
    }

    private static bool ReopenVerifyDeviceChannelMap(string project, string editor, string deviceText,
        string[] expectedChannels, string[] expectedVariables, string outDir, TimeSpan timeout,
        out DeviceChannelRow[] reopenRows)
    {
        reopenRows = Array.Empty<DeviceChannelRow>();
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        try
        {
            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) throw new InvalidOperationException("Failed to reopen editor.");
            main = WaitForMainWindow(process.Id, timeout);
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window was not found during reopen verification.");

            var tree = EnterDeviceConfiguration(process.Id, main, deviceText, timeout);
            var deviceDialog = OpenDeviceEditor(process.Id, tree, deviceText, timeout);
            var list = FindDeviceChannelList(deviceDialog);
            var rows = ReadDeviceChannelRows(list);
            reopenRows = rows;
            WriteDeviceRows(Path.Combine(outDir, "channels-reopen.json"), rows);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "05-reopen-verify"));

            var channelsOk = expectedChannels.All(expected =>
                rows.Any(row => row.Texts.Any(t => t.Equals(expected, StringComparison.OrdinalIgnoreCase))));
            var variablesOk = expectedVariables.Length == 0 ||
                              expectedVariables.All(expected =>
                                  rows.Any(row => row.Texts.Any(t => t.Equals(expected, StringComparison.OrdinalIgnoreCase))));
            return channelsOk && variablesOk;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    CloseEditorProcess(process.Id, main, saveIntent: false);
                }
                catch { }
            }
        }
    }

    private static MceSnapshot ReopenProjectAndExportSnapshot(string project, string editor, string outDir,
        string captureName, string exportName, TimeSpan timeout)
    {
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        try
        {
            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) throw new InvalidOperationException("Failed to reopen editor.");
            main = WaitForMainWindow(process.Id, timeout);
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window was not found during reopen verification.");
            CaptureProcessWindows(process.Id, Path.Combine(outDir, captureName));
            CloseEditorProcess(process.Id, main, saveIntent: false);
            process = null;
            main = IntPtr.Zero;
            Thread.Sleep(500);
            return ExportMceSnapshot(project, Path.Combine(outDir, exportName));
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saveIntent: false); } catch { }
            }
        }
    }

    private static bool ReopenVerifyMomentaryButton(string project, string editor, string label, string variable,
        int windowIndex, int x, int y, int width, int height, string outDir, TimeSpan timeout,
        MceSnapshot afterSaveSnapshot, out bool tokenReopenVerified)
    {
        tokenReopenVerified = false;
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        try
        {
            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) throw new InvalidOperationException("Failed to reopen editor.");
            main = WaitForMainWindow(process.Id, timeout);
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window was not found during momentary readback.");

            UiAutomation.SendCommand(main, 33955);
            Thread.Sleep(700);
            var userList = FindListViewByItemCount(main, 3);
            UiAutomation.ListViewSelectIndex(userList, windowIndex);
            Thread.Sleep(250);
            if (!ClickButtonByNormalizedText(main, mouse: true, "动画组态"))
                throw new InvalidOperationException("动画组态 button was not found during momentary readback.");
            Thread.Sleep(1000);

            var canvas = FindCanvas(main);
            UiAutomation.ClickPoint(canvas, x + width / 2, y + height / 2, MouseButton.Left, doubleClick: false, mouse: true);
            Thread.Sleep(300);
            UiAutomation.SendCommand(main, 32785);
            var dialog = UiAutomation.WaitForWindow(process.Id, "标准按钮构件属性设置", "#32770", TimeSpan.FromSeconds(8));
            if (dialog == IntPtr.Zero) throw new TimeoutException("Button property dialog was not found during momentary readback.");
            Thread.Sleep(300);

            var tab = FindFirstChild(dialog, "SysTabControl32", null);
            UiAutomation.TabSelectIndex(tab, 1, mouse: true);
            Thread.Sleep(400);
            var pressReadback = VerifyMomentaryOperation(dialog, "按下功能", "置1", variable);
            var releaseReadback = VerifyMomentaryOperation(dialog, "抬起功能", "清0", variable);
            var pressOk = pressReadback.Passed;
            var releaseOk = releaseReadback.Passed;
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "reopen-momentary-operation-readback"));

            UiAutomation.TabSelectIndex(tab, 2, mouse: true);
            Thread.Sleep(300);
            var scriptText = Native.GetText(FindLargestEdit(dialog));
            var scriptEmpty = string.IsNullOrWhiteSpace(scriptText);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "reopen-momentary-readback"));

            UiAutomation.CloseWindow(dialog);
            Thread.Sleep(300);
            CloseEditorProcess(process.Id, main, saveIntent: false);
            process = null;
            main = IntPtr.Zero;

            var reopenSnapshot = ExportMceSnapshot(project, Path.Combine(outDir, "mce-reopen"));
            tokenReopenVerified = ReopenTokenCountsPreserved(afterSaveSnapshot, reopenSnapshot, new[] { label, variable });
            File.WriteAllText(Path.Combine(outDir, "momentary-readback.json"),
                JsonSerializer.Serialize(new
                {
                    label,
                    variable,
                    pressOk,
                    releaseOk,
                    pressReadback,
                    releaseReadback,
                    scriptEmpty,
                    tokenReopenVerified
                }, JsonOptions()), Encoding.UTF8);
            return pressOk && releaseOk && scriptEmpty;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saveIntent: false); } catch { }
            }
        }
    }

    private static bool ReopenVerifyStatusButtonIndicator(string project, string editor, string label, string expression,
        int windowIndex, int x, int y, int width, int height, string outDir, TimeSpan timeout)
    {
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        try
        {
            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) throw new InvalidOperationException("Failed to reopen editor.");
            main = WaitForMainWindow(process.Id, timeout);
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window was not found during indicator readback.");

            UiAutomation.SendCommand(main, 33955);
            Thread.Sleep(700);
            var userList = FindListViewByItemCount(main, 3);
            UiAutomation.ListViewSelectIndex(userList, windowIndex);
            Thread.Sleep(250);
            if (!ClickButtonByNormalizedText(main, mouse: true, "\u52a8\u753b\u7ec4\u6001"))
                throw new InvalidOperationException("animation edit button was not found during indicator readback.");
            Thread.Sleep(1000);

            var canvas = FindCanvas(main);
            UiAutomation.ClickPoint(canvas, x + width / 2, y + height / 2, MouseButton.Left, doubleClick: false, mouse: true);
            Thread.Sleep(300);
            UiAutomation.SendCommand(main, 32785);
            var dialog = UiAutomation.WaitForWindow(process.Id, "\u6807\u51c6\u6309\u94ae\u6784\u4ef6\u5c5e\u6027\u8bbe\u7f6e", "#32770", TimeSpan.FromSeconds(8));
            if (dialog == IntPtr.Zero) throw new TimeoutException("Button property dialog was not found during indicator readback.");
            Thread.Sleep(300);

            var tab = FindFirstChild(dialog, "SysTabControl32", null);
            UiAutomation.TabSelectIndex(tab, 0, mouse: true);
            Thread.Sleep(250);
            var labelOk = UiAutomation.EnumerateChildren(dialog)
                .Where(h => Native.GetClass(h).Contains("Edit", StringComparison.OrdinalIgnoreCase))
                .Any(h => string.Equals(Native.GetText(h), label, StringComparison.Ordinal));

            UiAutomation.TabSelectIndex(tab, 3, mouse: true);
            Thread.Sleep(350);
            var expressionText = Native.GetText(FindVisibilityExpressionEdit(dialog));
            var expressionOk = string.Equals(expressionText, expression, StringComparison.Ordinal);

            UiAutomation.TabSelectIndex(tab, 1, mouse: true);
            Thread.Sleep(300);
            var dataOperation = FindButtonByNormalizedText(dialog, "\u6570\u636e\u5bf9\u8c61\u503c\u64cd\u4f5c");
            var noOperation = dataOperation == IntPtr.Zero || UiAutomation.ButtonGetCheck(dataOperation) == 0;

            UiAutomation.TabSelectIndex(tab, 2, mouse: true);
            Thread.Sleep(300);
            var scriptText = Native.GetText(FindLargestEdit(dialog));
            var scriptEmpty = string.IsNullOrWhiteSpace(scriptText);

            CaptureProcessWindows(process.Id, Path.Combine(outDir, "reopen-indicator-readback"));
            File.WriteAllText(Path.Combine(outDir, "indicator-readback.json"),
                JsonSerializer.Serialize(new
                {
                    label,
                    expression,
                    labelOk,
                    expressionText,
                    expressionOk,
                    noOperation,
                    scriptEmpty
                }, JsonOptions()), Encoding.UTF8);

            UiAutomation.CloseWindow(dialog);
            Thread.Sleep(300);
            CloseEditorProcess(process.Id, main, saveIntent: false);
            process = null;
            main = IntPtr.Zero;
            return labelOk && expressionOk && noOperation && scriptEmpty;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "indicator-readback-error.txt"), ex.ToString(), Encoding.UTF8);
            return false;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saveIntent: false); } catch { }
            }
        }
    }

    private static void OpenAnimationConfiguration(int pid, IntPtr main, int windowIndex, string context)
    {
        UiAutomation.SendCommand(main, 33955);
        Thread.Sleep(700);
        var userList = FindListViewByItemCount(main, 3);
        UiAutomation.ListViewSelectIndex(userList, windowIndex);
        Thread.Sleep(250);
        if (!ClickButtonByNormalizedText(main, mouse: true, AnimationEditButtonText))
            throw new InvalidOperationException("Animation configuration button was not found for " + context + ".");
        Thread.Sleep(1000);
        RecordOpenPopups(pid, "animation-config." + SafeFile(context));
    }

    private static IntPtr OpenCanvasObjectPropertyDialog(int pid, IntPtr main, IntPtr canvas,
        int x, int y, int width, int height, string expectedTitle, bool preferCommand)
    {
        IntPtr TryExpected(TimeSpan timeout)
            => WaitForTopWindow(pid,
                h => Native.GetClass(h) == "#32770" &&
                     Native.IsWindowVisible(h) &&
                     Native.GetText(h).Contains(expectedTitle, StringComparison.OrdinalIgnoreCase),
                timeout);

        void CloseWrongDialogs(string state)
        {
            foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h =>
                         Native.GetClass(h) == "#32770" &&
                         Native.IsWindowVisible(h) &&
                         !Native.GetText(h).Contains(expectedTitle, StringComparison.OrdinalIgnoreCase)))
            {
                RecordDialogEvidence("dialogs.jsonl", pid, dialog, "close-unexpected", state);
                UiAutomation.CloseWindow(dialog);
                Thread.Sleep(250);
            }
        }

        var cx = x + width / 2;
        var cy = y + height / 2;
        UiAutomation.ClickPoint(canvas, cx, cy, MouseButton.Left, doubleClick: false, mouse: true);
        Thread.Sleep(250);

        if (preferCommand)
        {
            UiAutomation.SendCommand(main, 32785);
            var byCommand = TryExpected(TimeSpan.FromSeconds(3));
            if (byCommand != IntPtr.Zero) return byCommand;
            CloseWrongDialogs("object-property.command");
        }

        UiAutomation.ClickPoint(canvas, cx, cy, MouseButton.Left, doubleClick: true, mouse: true);
        var byDoubleClick = TryExpected(TimeSpan.FromSeconds(4));
        if (byDoubleClick != IntPtr.Zero) return byDoubleClick;
        CloseWrongDialogs("object-property.double-click");

        if (!preferCommand)
        {
            UiAutomation.ClickPoint(canvas, cx, cy, MouseButton.Left, doubleClick: false, mouse: true);
            Thread.Sleep(250);
            UiAutomation.SendCommand(main, 32785);
            var byFallbackCommand = TryExpected(TimeSpan.FromSeconds(3));
            if (byFallbackCommand != IntPtr.Zero) return byFallbackCommand;
            CloseWrongDialogs("object-property.fallback-command");
        }

        throw new TimeoutException("Expected object property dialog was not found: " + expectedTitle);
    }

    private static void ConfigureNativeStaticText(IntPtr dialog, string label)
    {
        var tab = FindFirstChild(dialog, "SysTabControl32", null);
        UiAutomation.TabSelectIndex(tab, 1, mouse: true);
        Thread.Sleep(350);
        var edit = FindNativeStaticTextEdit(dialog);
        SetTextAndVerify(edit, label, "native static text");
    }

    private static void ConfigureNativeLamp(IntPtr dialog, string label, string expression)
    {
        var tab = FindFirstChild(dialog, "SysTabControl32", null);

        UiAutomation.TabSelectIndex(tab, 0, mouse: true);
        Thread.Sleep(350);
        var textEdit = FindNativeLampTextEdit(dialog);
        SetTextAndVerify(textEdit, label, "native lamp display text");

        UiAutomation.TabSelectIndex(tab, 1, mouse: true);
        Thread.Sleep(350);
        var variableEdit = FindNativeLampDisplayVariableEdit(dialog);
        SetTextAndVerify(variableEdit, expression, "native lamp display variable");
    }

    private static bool ReopenVerifyNativeStaticText(string project, string editor, string label,
        int windowIndex, int x, int y, int width, int height, string outDir, TimeSpan timeout)
    {
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        try
        {
            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) throw new InvalidOperationException("Failed to reopen editor.");
            main = WaitForMainWindow(process.Id, timeout);
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window was not found during native static text readback.");

            OpenAnimationConfiguration(process.Id, main, windowIndex, "native static text readback");
            var canvas = FindCanvas(main);
            var dialog = OpenCanvasObjectPropertyDialog(process.Id, main, canvas, x, y, width, height,
                NativeStaticTextDialogTitle, preferCommand: true);
            var tab = FindFirstChild(dialog, "SysTabControl32", null);
            UiAutomation.TabSelectIndex(tab, 1, mouse: true);
            Thread.Sleep(350);
            var text = Native.GetText(FindNativeStaticTextEdit(dialog));
            var labelOk = string.Equals(text, label, StringComparison.Ordinal);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "reopen-native-static-text-readback"));
            File.WriteAllText(Path.Combine(outDir, "native-static-text-readback.json"),
                JsonSerializer.Serialize(new { label, text, labelOk }, JsonOptions()), Encoding.UTF8);

            UiAutomation.CloseWindow(dialog);
            Thread.Sleep(300);
            CloseEditorProcess(process.Id, main, saveIntent: false);
            process = null;
            main = IntPtr.Zero;
            return labelOk;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "native-static-text-readback-error.txt"), ex.ToString(), Encoding.UTF8);
            return false;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saveIntent: false); } catch { }
            }
        }
    }

    private static bool ReopenVerifyNativeLamp(string project, string editor, string label, string expression,
        int windowIndex, int x, int y, int width, int height, string outDir, TimeSpan timeout)
    {
        Process? process = null;
        IntPtr main = IntPtr.Zero;
        try
        {
            process = Process.Start(new ProcessStartInfo(editor, Quote(project))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            });
            if (process == null) throw new InvalidOperationException("Failed to reopen editor.");
            main = WaitForMainWindow(process.Id, timeout);
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window was not found during native lamp readback.");

            OpenAnimationConfiguration(process.Id, main, windowIndex, "native lamp readback");
            var canvas = FindCanvas(main);
            var dialog = OpenCanvasObjectPropertyDialog(process.Id, main, canvas, x, y, width, height,
                NativeLampDialogTitle, preferCommand: false);
            var tab = FindFirstChild(dialog, "SysTabControl32", null);

            UiAutomation.TabSelectIndex(tab, 0, mouse: true);
            Thread.Sleep(350);
            var text = Native.GetText(FindNativeLampTextEdit(dialog));
            var labelOk = string.Equals(text, label, StringComparison.Ordinal);

            UiAutomation.TabSelectIndex(tab, 1, mouse: true);
            Thread.Sleep(350);
            var variable = Native.GetText(FindNativeLampDisplayVariableEdit(dialog));
            var variableOk = string.Equals(variable, expression, StringComparison.Ordinal);

            CaptureProcessWindows(process.Id, Path.Combine(outDir, "reopen-native-lamp-readback"));
            File.WriteAllText(Path.Combine(outDir, "native-lamp-readback.json"),
                JsonSerializer.Serialize(new
                {
                    label,
                    text,
                    labelOk,
                    expression,
                    variable,
                    variableOk
                }, JsonOptions()), Encoding.UTF8);

            UiAutomation.CloseWindow(dialog);
            Thread.Sleep(300);
            CloseEditorProcess(process.Id, main, saveIntent: false);
            process = null;
            main = IntPtr.Zero;
            return labelOk && variableOk;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "native-lamp-readback-error.txt"), ex.ToString(), Encoding.UTF8);
            return false;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saveIntent: false); } catch { }
            }
        }
    }

    private static void SetTextAndVerify(IntPtr edit, string value, string role)
    {
        UiAutomation.SetControlText(edit, value, paste: true);
        Thread.Sleep(180);
        if (!string.Equals(Native.GetText(edit), value, StringComparison.Ordinal))
        {
            PasteTextWithKeyboard(edit, value);
            Thread.Sleep(180);
        }
        if (!string.Equals(Native.GetText(edit), value, StringComparison.Ordinal))
            throw new InvalidOperationException(role + " edit readback did not match requested text.");
    }

    private static IntPtr FindNativeStaticTextEdit(IntPtr dialog)
    {
        var dialogRect = UiAutomation.GetWindowRect(dialog);
        var edit = VisibleEdits(dialog)
            .Where(e =>
                e.Rect.Width >= 180 &&
                e.Rect.Height >= 70 &&
                e.Rect.Left > dialogRect.Left + 10 &&
                e.Rect.Top > dialogRect.Top + 20 &&
                e.Rect.Top < dialogRect.Top + 180)
            .OrderByDescending(e => e.Rect.Width * e.Rect.Height)
            .Select(e => e.Handle)
            .FirstOrDefault();
        if (edit == IntPtr.Zero) throw new InvalidOperationException("Native static text Edit was not found.");
        return edit;
    }

    private static IntPtr FindNativeLampTextEdit(IntPtr dialog)
    {
        var dialogRect = UiAutomation.GetWindowRect(dialog);
        var edit = VisibleEdits(dialog)
            .Where(e =>
                e.Rect.Width >= 200 &&
                e.Rect.Height >= 50 &&
                e.Rect.Top > dialogRect.Top + 180 &&
                e.Rect.Top < dialogRect.Top + 360)
            .OrderByDescending(e => e.Rect.Width * e.Rect.Height)
            .Select(e => e.Handle)
            .FirstOrDefault();
        if (edit == IntPtr.Zero) throw new InvalidOperationException("Native lamp text Edit was not found.");
        return edit;
    }

    private static IntPtr FindNativeLampDisplayVariableEdit(IntPtr dialog)
    {
        var dialogRect = UiAutomation.GetWindowRect(dialog);
        var edit = VisibleEdits(dialog)
            .Where(e =>
                e.Rect.Width >= 70 &&
                e.Rect.Width <= 180 &&
                e.Rect.Height <= 35 &&
                e.Rect.Left > dialogRect.Left + 150 &&
                e.Rect.Top > dialogRect.Top + 40 &&
                e.Rect.Top < dialogRect.Top + 140)
            .OrderByDescending(e => e.Rect.Width)
            .Select(e => e.Handle)
            .FirstOrDefault();
        if (edit == IntPtr.Zero) throw new InvalidOperationException("Native lamp display variable Edit was not found.");
        return edit;
    }

    private static IEnumerable<(IntPtr Handle, Rect Rect)> VisibleEdits(IntPtr root)
        => UiAutomation.EnumerateChildren(root)
            .Where(h => Native.GetClass(h).Equals("Edit", StringComparison.OrdinalIgnoreCase) && Native.IsWindowVisible(h))
            .Select(h => (Handle: h, Rect: UiAutomation.GetWindowRect(h)));

    private sealed record MomentaryOperationReadback(
        string SubTab,
        string ExpectedOperation,
        string ExpectedVariable,
        bool TabClicked,
        int CheckboxState,
        bool CheckedOk,
        int ComboCurrentIndex,
        string SelectedOperation,
        string[] ComboItems,
        bool OperationOk,
        string VariableText,
        bool VariableOk,
        bool Passed,
        string? Error);

    private static MomentaryOperationReadback VerifyMomentaryOperation(IntPtr dialog, string subTab, string operation, string variable)
    {
        if (!ClickButtonByNormalizedText(dialog, mouse: true, subTab))
            return new MomentaryOperationReadback(subTab, operation, variable, false, 0, false, -1, "",
                Array.Empty<string>(), false, "", false, false, "sub-tab button was not found");
        Thread.Sleep(250);
        try
        {
            var checkbox = FindButtonByNormalizedText(dialog, "数据对象值操作");
            var checkboxState = checkbox == IntPtr.Zero ? 0 : UiAutomation.ButtonGetCheck(checkbox);
            var checkedOk = checkbox != IntPtr.Zero && checkboxState != 0;
            var combo = FindVisibleComboWithItem(dialog, operation);
            var comboItems = UiAutomation.ComboItems(combo);
            var currentIndex = UiAutomation.ComboCurrentIndex(combo);
            var selectedOperation = comboItems.FirstOrDefault(item => item.Index == currentIndex)?.Text ?? Native.GetText(combo);
            var operationOk = selectedOperation.Contains(operation, StringComparison.OrdinalIgnoreCase);
            var edit = FindEditRightOf(dialog, combo);
            var variableText = Native.GetText(edit);
            var variableOk = string.Equals(variableText, variable, StringComparison.Ordinal);
            var passed = checkedOk && operationOk && variableOk;
            return new MomentaryOperationReadback(subTab, operation, variable, true, checkboxState, checkedOk,
                currentIndex, selectedOperation, comboItems.Select(item => item.Text).ToArray(), operationOk,
                variableText, variableOk, passed, null);
        }
        catch (Exception ex)
        {
            return new MomentaryOperationReadback(subTab, operation, variable, true, 0, false, -1, "",
                Array.Empty<string>(), false, "", false, false, ex.Message);
        }
    }

    private static IntPtr FindButtonByNormalizedText(IntPtr root, params string[] labels)
    {
        var targets = labels
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(CompactLabel)
            .Where(s => s.Length > 0)
            .ToArray();
        var buttons = UiAutomation.EnumerateChildren(root)
            .Where(h => Native.GetClass(h).Contains("Button", StringComparison.OrdinalIgnoreCase) && Native.IsWindowVisible(h))
            .Select(h => new { Handle = h, Compact = CompactLabel(Native.GetText(h)) })
            .ToArray();
        return targets
                   .Select(target => buttons.FirstOrDefault(b => b.Compact.Equals(target, StringComparison.OrdinalIgnoreCase)))
                   .FirstOrDefault(button => button != null)?.Handle
               ?? targets
                   .Select(target => buttons.FirstOrDefault(b => b.Compact.Contains(target, StringComparison.OrdinalIgnoreCase)))
                   .FirstOrDefault(button => button != null)?.Handle
               ?? IntPtr.Zero;
    }

    private static bool ReopenTokenCountsPreserved(MceSnapshot afterSave, MceSnapshot afterReopen, IEnumerable<string> tokens)
        => tokens.Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .All(token => afterSave.CountBlobToken(token) > 0 &&
                          afterReopen.CountBlobToken(token) >= afterSave.CountBlobToken(token));

    private static void CloseEditorProcess(int pid, IntPtr main, bool saveIntent)
    {
        var hwnd = main != IntPtr.Zero ? main : UiAutomation.FindMainWindow(pid);
        foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h => h != hwnd && Native.GetClass(h) == "#32770"))
        {
            UiAutomation.CloseWindow(dialog);
            Thread.Sleep(200);
        }
        if (hwnd != IntPtr.Zero)
        {
            if (saveIntent) UiAutomation.SendCommand(hwnd, SaveCommandId);
            Thread.Sleep(500);
            UiAutomation.CloseWindow(hwnd);
            UiAutomation.HandleCloseDialogs(pid, saveIntent, TimeSpan.FromSeconds(12));
        }
    }

    private static IntPtr FindListViewByItemCount(IntPtr root, int count)
    {
        foreach (var child in UiAutomation.EnumerateChildren(root))
        {
            if (!Native.GetClass(child).Contains("SysListView32", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (UiAutomation.ListViewItems(child).Length == count) return child;
            }
            catch
            {
                // Ignore nonstandard list views while profiling.
            }
        }
        throw new InvalidOperationException($"ListView with {count} rows was not found.");
    }

    private static IntPtr FindCanvas(IntPtr root)
    {
        var canvas = UiAutomation.EnumerateChildren(root)
            .FirstOrDefault(h =>
            {
                if (!Native.GetClass(h).Equals("Afx:400000:100b:0:6:0", StringComparison.OrdinalIgnoreCase)) return false;
                var r = UiAutomation.GetWindowRect(h);
                return r.Width > 500 && r.Height > 250;
            });
        if (canvas == IntPtr.Zero) throw new InvalidOperationException("Animation canvas was not found.");
        return canvas;
    }

    private static IntPtr FindFirstChild(IntPtr root, string className, string? text)
    {
        var child = UiAutomation.EnumerateChildren(root).FirstOrDefault(h =>
            Native.GetClass(h).Contains(className, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(text) || Native.GetText(h).Contains(text, StringComparison.OrdinalIgnoreCase)));
        if (child == IntPtr.Zero) throw new InvalidOperationException($"Child not found: class={className}, text={text}");
        return child;
    }

    private static IntPtr FindBaseButtonTextEdit(IntPtr dialog)
    {
        var dialogRect = UiAutomation.GetWindowRect(dialog);
        var edit = UiAutomation.EnumerateChildren(dialog)
            .Where(h => Native.GetClass(h).Equals("Edit", StringComparison.OrdinalIgnoreCase))
            .Select(h => new { Handle = h, Rect = UiAutomation.GetWindowRect(h) })
            .Where(e =>
                e.Rect.Width >= 120 && e.Rect.Width <= 180 &&
                e.Rect.Height >= 70 && e.Rect.Height <= 110 &&
                e.Rect.Left > dialogRect.Left + 50 &&
                e.Rect.Top < dialogRect.Top + 200)
            .OrderBy(e => e.Rect.Top)
            .Select(e => e.Handle)
            .FirstOrDefault();
        if (edit == IntPtr.Zero) throw new InvalidOperationException("Base button text Edit was not found.");
        return edit;
    }

    private static void ConfigureStandardButtonText(IntPtr dialog, string label)
    {
        var tab = FindFirstChild(dialog, "SysTabControl32", null);
        UiAutomation.TabSelectIndex(tab, 0, mouse: true);
        Thread.Sleep(300);

        foreach (var stateButton in new[] { "\u62ac\u8d77\u72b6\u6001", "\u6309\u4e0b\u72b6\u6001" })
        {
            ClickButtonByNormalizedText(dialog, mouse: true, stateButton);
            Thread.Sleep(150);
            var edit = FindBaseButtonTextEdit(dialog);
            UiAutomation.SetControlText(edit, label, paste: true);
            Thread.Sleep(150);
            var actual = Native.GetText(edit);
            if (!string.Equals(actual, label, StringComparison.Ordinal))
            {
                PasteTextWithKeyboard(edit, label);
                Thread.Sleep(150);
            }
        }
    }

    private static void PasteTextWithKeyboard(IntPtr edit, string text)
    {
        Native.SetForegroundWindow(Native.GetAncestor(edit, Native.GA_ROOT));
        Native.SetFocus(edit);
        var rect = UiAutomation.GetWindowRect(edit);
        UiAutomation.ClickPoint(edit, Math.Max(1, rect.Width / 2), Math.Max(1, rect.Height / 2),
            MouseButton.Left, doubleClick: false, mouse: true);
        Thread.Sleep(80);
        Clipboard.SetText(text ?? "");
        SendKeys.SendWait("^a");
        Thread.Sleep(80);
        SendKeys.SendWait("^v");
        Thread.Sleep(150);
    }

    private static IntPtr FindLargestEdit(IntPtr root)
    {
        var edit = UiAutomation.EnumerateChildren(root)
            .Where(h => Native.GetClass(h).Equals("Edit", StringComparison.OrdinalIgnoreCase))
            .Select(h => new { Handle = h, Rect = UiAutomation.GetWindowRect(h) })
            .OrderByDescending(e => e.Rect.Width * e.Rect.Height)
            .Select(e => e.Handle)
            .FirstOrDefault();
        if (edit == IntPtr.Zero) throw new InvalidOperationException("Edit control was not found.");
        return edit;
    }

    private static IntPtr FindVisibilityExpressionEdit(IntPtr dialog)
    {
        var dialogRect = UiAutomation.GetWindowRect(dialog);
        var edit = UiAutomation.EnumerateChildren(dialog)
            .Where(h => Native.GetClass(h).Equals("Edit", StringComparison.OrdinalIgnoreCase))
            .Select(h => new { Handle = h, Rect = UiAutomation.GetWindowRect(h) })
            .Where(e =>
                e.Rect.Width >= 250 &&
                e.Rect.Height <= 35 &&
                e.Rect.Left > dialogRect.Left + 30 &&
                e.Rect.Top > dialogRect.Top + 80 &&
                e.Rect.Top < dialogRect.Top + 150)
            .OrderByDescending(e => e.Rect.Width)
            .Select(e => e.Handle)
            .FirstOrDefault();
        if (edit == IntPtr.Zero) throw new InvalidOperationException("Visibility expression Edit was not found.");
        return edit;
    }

    private static void HandlePropertyConfirmDialogs(int pid, IntPtr ownerDialog, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var handled = false;
            foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h =>
                         h != ownerDialog && Native.GetClass(h) == "#32770"))
            {
                var text = DialogText(dialog);
                if (IsUnknownObjectDialogText(text) || IsDialogErrorText(text))
                {
                    RecordDialogEvidence("dialogs.jsonl", pid, dialog, "fail", "property.confirm");
                    throw new InvalidOperationException("Refusing to auto-confirm property dialog: " + ShortDialogText(dialog));
                }
                if (!IsBenignInfoDialogText(text))
                {
                    RecordDialogEvidence("dialogs.jsonl", pid, dialog, "fail", "property.confirm");
                    throw new InvalidOperationException("Unexpected property dialog: " + ShortDialogText(dialog));
                }
                RecordDialogEvidence("dialogs.jsonl", pid, dialog, "click-ok", "property.confirm");
                ClickButtonByNormalizedText(dialog, mouse: true, "确定", "确认", "是(&Y)", "是");
                handled = true;
            }

            if (!handled) break;
            Thread.Sleep(500);
        }
        RecordOpenPopups(pid, "property.confirm");
    }

    private static void HandleScriptCheckDialogs(int pid, IntPtr scriptDialog, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var handled = false;
            foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h =>
                         h != scriptDialog && Native.GetClass(h) == "#32770"))
            {
                var text = DialogText(dialog);
                if (IsUnknownObjectDialogText(text) || IsDialogErrorText(text))
                {
                    RecordDialogEvidence("dialogs.jsonl", pid, dialog, "fail", "script.check");
                    throw new InvalidOperationException("Script check failed or referenced unknown objects: " + ShortDialogText(dialog));
                }
                if (!IsBenignInfoDialogText(text))
                {
                    RecordDialogEvidence("dialogs.jsonl", pid, dialog, "fail", "script.check");
                    throw new InvalidOperationException("Unexpected script check dialog: " + ShortDialogText(dialog));
                }
                RecordDialogEvidence("dialogs.jsonl", pid, dialog, "click-ok", "script.check");
                ClickButtonByNormalizedText(dialog, mouse: true, "确定", "确认", "是(Y)", "是");
                handled = true;
            }

            if (!handled) break;
            Thread.Sleep(400);
        }
        RecordOpenPopups(pid, "script.check");
    }

    private static void ConfirmScriptDialog(int pid, IntPtr scriptDialog, IntPtr ownerDialog, bool allowCreateDataObjects)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (!Native.IsWindow(scriptDialog) || !Native.IsWindowVisible(scriptDialog)) return;
            if (!ClickButtonByNormalizedText(scriptDialog, mouse: true, "确定(Y)", "确定(&Y)", "确定"))
                throw new InvalidOperationException("Script editor OK button was not found.");
            HandleScriptConfirmDialogs(pid, scriptDialog, ownerDialog, TimeSpan.FromSeconds(8), allowCreateDataObjects);
            WaitForWindowClosed(scriptDialog, TimeSpan.FromSeconds(3));
        }

        if (Native.IsWindow(scriptDialog) && Native.IsWindowVisible(scriptDialog))
            throw new InvalidOperationException("Script editor did not close after confirmation.");
    }

    private static void HandleScriptConfirmDialogs(int pid, IntPtr scriptDialog, IntPtr ownerDialog, TimeSpan timeout, bool allowCreateDataObjects)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (!allowCreateDataObjects && UiAutomation.TopWindowsForPid(pid).Any(h =>
                    h != scriptDialog && h != ownerDialog && Native.GetClass(h) == "#32770"))
            {
                foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h =>
                             h != scriptDialog && h != ownerDialog && Native.GetClass(h) == "#32770"))
                    RecordDialogEvidence("dialogs.jsonl", pid, dialog, "fail", "script.confirm");
                throw new InvalidOperationException(
                    "Script confirmation opened a secondary MCGS dialog. Refusing to auto-confirm it without --allow-create-dataobjects.");
            }

            var handled = false;
            foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h =>
                         h != scriptDialog && h != ownerDialog && Native.GetClass(h) == "#32770"))
            {
                var text = DialogText(dialog);
                if (ContainsAny(text, "未知对象", "是否增加此对象", "组态错误"))
                {
                    RecordDialogEvidence("dialogs.jsonl", pid, dialog, "click-yes", "script.confirm-create-dataobject");
                    ClickButtonByNormalizedText(dialog, mouse: true, "是(Y)", "是", "确定", "确认");
                    handled = true;
                    continue;
                }

                if (ContainsAny(text, "错误", "检查", "提示", "成功"))
                {
                    RecordDialogEvidence("dialogs.jsonl", pid, dialog, "click-ok", "script.confirm-info");
                    ClickButtonByNormalizedText(dialog, mouse: true, "确定", "确认", "是(Y)", "是");
                    handled = true;
                }
            }

            if (!handled) break;
            Thread.Sleep(500);
        }
        RecordOpenPopups(pid, "script.confirm");
    }

    private static void ConfigureMomentaryOperation(int pid, IntPtr dialog, string subTab, string comboText, string variable,
        string outDir)
    {
        if (!UiAutomation.ClickButtonByText(dialog, subTab, mouse: true))
            throw new InvalidOperationException(subTab + " button was not found.");
        Thread.Sleep(250);
        if (!UiAutomation.ClickButtonByText(dialog, "数据对象值操作", mouse: true))
            throw new InvalidOperationException("数据对象值操作 checkbox was not found.");
        Thread.Sleep(250);

        var combo = FindComboWithItem(dialog, comboText);
        UiAutomation.ComboSelectText(combo, comboText);
        Thread.Sleep(100);
        var edit = FindEditRightOf(dialog, combo);
        UiAutomation.SetControlText(edit, variable, paste: true);
        SelectDataObjectWithPicker(pid, dialog, edit, variable, outDir, subTab);
        Thread.Sleep(150);
    }

    private static void SelectDataObjectWithPicker(int pid, IntPtr ownerDialog, IntPtr edit, string variable,
        string outDir, string subTab)
    {
        var selectorButton = FindQuestionButtonRightOf(ownerDialog, edit);
        if (selectorButton == IntPtr.Zero)
            throw new InvalidOperationException("Data-object selector button was not found for " + subTab + ".");

        var selectorRect = UiAutomation.GetWindowRect(selectorButton);
        UiAutomation.ClickPoint(selectorButton, Math.Max(1, selectorRect.Width / 2), Math.Max(1, selectorRect.Height / 2),
            MouseButton.Left, doubleClick: false, mouse: true);
        var picker = WaitForTopWindow(pid,
            h => h != ownerDialog && Native.GetClass(h) == "#32770" && Native.IsWindowVisible(h),
            TimeSpan.FromSeconds(8));
        if (picker == IntPtr.Zero)
            throw new TimeoutException("Data-object selector dialog was not found for " + subTab + ".");

        Thread.Sleep(300);
        SelectDataObjectInPicker(picker, variable);
        Thread.Sleep(300);
        if (!Native.IsWindow(picker) || !Native.IsWindowVisible(picker))
            return;
        CaptureProcessWindows(pid, Path.Combine(outDir, "data-object-picker-" + SafeFile(subTab)));
        if (!ClickButtonByNormalizedText(picker, mouse: true, "确认(Y)", "确认(&Y)", "确定", "确认", "OK"))
            throw new InvalidOperationException("Data-object selector confirm button was not found.");
        WaitForWindowClosed(picker, TimeSpan.FromSeconds(8));
        Thread.Sleep(250);
    }

    private static void SelectDataObjectInPicker(IntPtr picker, string variable)
    {
        foreach (var list in UiAutomation.EnumerateChildren(picker).Where(h =>
                     Native.GetClass(h).Equals("SysListView32", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                UiAutomation.ListViewDoubleClickText(list, variable, mouse: true);
                return;
            }
            catch
            {
                // Try the next picker control.
            }
        }

        foreach (var list in UiAutomation.EnumerateChildren(picker).Where(h =>
                     Native.GetClass(h).Contains("ListBox", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                UiAutomation.ListBoxSelectText(list, variable);
                return;
            }
            catch
            {
                // Try the next picker control.
            }
        }

        foreach (var tree in UiAutomation.EnumerateChildren(picker).Where(h =>
                     Native.GetClass(h).Equals("SysTreeView32", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                UiAutomation.TreeViewSelectText(tree, variable);
                return;
            }
            catch
            {
                // Try the next picker control.
            }
        }

        var edit = UiAutomation.EnumerateChildren(picker)
            .FirstOrDefault(h => Native.GetClass(h).Equals("Edit", StringComparison.OrdinalIgnoreCase));
        if (edit != IntPtr.Zero)
        {
            UiAutomation.SetControlText(edit, variable, paste: true);
            return;
        }

        throw new InvalidOperationException("Data-object selector did not expose a selectable variable control for " + variable + ".");
    }

    private static IntPtr FindQuestionButtonRightOf(IntPtr root, IntPtr edit)
    {
        var editRect = UiAutomation.GetWindowRect(edit);
        return UiAutomation.EnumerateChildren(root)
            .Where(h => Native.GetClass(h).Contains("Button", StringComparison.OrdinalIgnoreCase) &&
                        Native.GetText(h).Trim() == "?" &&
                        Native.IsWindowVisible(h))
            .Select(h => new { Handle = h, Rect = UiAutomation.GetWindowRect(h) })
            .Where(b => Math.Abs(b.Rect.Top - editRect.Top) <= 10 &&
                        b.Rect.Left >= editRect.Right - 2 &&
                        b.Rect.Left <= editRect.Right + 40)
            .OrderBy(b => b.Rect.Left)
            .Select(b => b.Handle)
            .FirstOrDefault();
    }

    private static IntPtr FindComboWithItem(IntPtr root, string text)
    {
        foreach (var combo in UiAutomation.EnumerateChildren(root).Where(h => Native.GetClass(h).Equals("ComboBox", StringComparison.OrdinalIgnoreCase)))
        {
            if (UiAutomation.ComboItems(combo).Any(i => i.Text.Contains(text, StringComparison.OrdinalIgnoreCase)))
                return combo;
        }
        throw new InvalidOperationException("Combo item not found: " + text);
    }

    private static IntPtr FindVisibleComboWithItem(IntPtr root, string text)
    {
        foreach (var combo in UiAutomation.EnumerateChildren(root).Where(h =>
                     Native.GetClass(h).Equals("ComboBox", StringComparison.OrdinalIgnoreCase) &&
                     Native.IsWindowVisible(h)))
        {
            if (UiAutomation.ComboItems(combo).Any(i => i.Text.Contains(text, StringComparison.OrdinalIgnoreCase)))
                return combo;
        }
        return FindComboWithItem(root, text);
    }

    private static IntPtr FindEditRightOf(IntPtr root, IntPtr combo)
    {
        var comboRect = UiAutomation.GetWindowRect(combo);
        var candidates = UiAutomation.EnumerateChildren(root)
            .Where(h => Native.GetClass(h).Equals("Edit", StringComparison.OrdinalIgnoreCase))
            .Select(h => new { Handle = h, Rect = UiAutomation.GetWindowRect(h) })
            .Where(e =>
                Math.Abs(e.Rect.Top - comboRect.Top) <= 8 &&
                e.Rect.Left > comboRect.Left &&
                e.Rect.Width >= 60 &&
                e.Rect.Width <= 160)
            .ToArray();
        var edit = candidates
            .Where(e => Native.IsWindowVisible(e.Handle))
            .OrderBy(e => e.Rect.Left)
            .Select(e => e.Handle)
            .FirstOrDefault();
        if (edit == IntPtr.Zero)
            edit = candidates
                .OrderBy(e => e.Rect.Left)
                .Select(e => e.Handle)
                .FirstOrDefault();
        if (edit == IntPtr.Zero) throw new InvalidOperationException("Edit right of operation combo was not found.");
        return edit;
    }

    private static void CaptureProcessWindows(int pid, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var windows = UiAutomation.TopWindowsForPid(pid).ToArray();
        var infos = new List<WindowInfo>();
        for (var i = 0; i < windows.Length; i++)
        {
            var hwnd = windows[i];
            var info = WindowInfo.FromHandle(hwnd);
            infos.Add(info);
            var prefix = $"{i:D2}-{SafeFile(info.ClassName)}-{SafeFile(info.Text)}";
            if (prefix.Length > 90) prefix = prefix[..90];
            File.WriteAllLines(Path.Combine(outDir, prefix + ".tree.txt"), UiAutomation.WindowTreeLines(hwnd), Encoding.UTF8);
            TryScreenshot(hwnd, Path.Combine(outDir, prefix + ".png"));
        }
        File.WriteAllText(Path.Combine(outDir, "windows.json"), JsonSerializer.Serialize(infos, JsonOptions()), Encoding.UTF8);
    }

    private static IntPtr ResolveMainWindow(string[] args)
    {
        var pid = OptInt(args, "--pid");
        if (pid.HasValue)
        {
            var hwnd = UiAutomation.FindMainWindow(pid.Value);
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("No visible MCGS window found for PID " + pid.Value);
            return hwnd;
        }

        if (!UiAutomation.TryFindLatestMainWindow(out var latest))
            throw new InvalidOperationException("No running MCGS editor window found. Use mcgsctl open first.");
        return latest;
    }

    private static IntPtr ResolveWindow(string[] args)
    {
        var hwndText = Opt(args, "--hwnd") ?? Opt(args, "--root");
        if (!string.IsNullOrWhiteSpace(hwndText))
        {
            return ParseHwnd(hwndText);
        }
        return ResolveMainWindow(args);
    }

    private static IntPtr ResolveCommandWindow(string[] args)
    {
        if (Has(args, "--hwnd")) return ResolveWindow(args);

        var main = ResolveMainWindow(args);
        var target = (Opt(args, "--target") ?? "main").ToLowerInvariant();
        return target switch
        {
            "main" => main,
            "mdi" => UiAutomation.GetMdiInfo(main).ActiveMdiHandle is string h && h != "0x0"
                ? ParseHwnd(h)
                : throw new InvalidOperationException("Active MDI child was not found."),
            "view" => UiAutomation.GetMdiInfo(main).ActiveViewHandle is string h && h != "0x0"
                ? ParseHwnd(h)
                : throw new InvalidOperationException("Active MDI view was not found."),
            _ => throw new ArgumentException("Unknown --target. Use main, mdi, or view.")
        };
    }

    private static IntPtr WaitForMainWindow(int pid, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var hwnd = UiAutomation.FindMainWindow(pid);
            if (hwnd != IntPtr.Zero) return hwnd;
            Thread.Sleep(250);
        }
        throw new TimeoutException("Timed out waiting for MCGS main window.");
    }

    internal static void TryScreenshot(IntPtr hwnd, string file)
    {
        try
        {
            var rect = UiAutomation.GetWindowRect(hwnd);
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using var bitmap = new Bitmap(rect.Width, rect.Height);
            using var graphics = Graphics.FromImage(bitmap);
            var hdc = graphics.GetHdc();
            try
            {
                if (!Native.PrintWindow(hwnd, hdc, 2))
                {
                    graphics.ReleaseHdc(hdc);
                    hdc = IntPtr.Zero;
                    graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(rect.Width, rect.Height));
                }
            }
            finally
            {
                if (hdc != IntPtr.Zero) graphics.ReleaseHdc(hdc);
            }
            bitmap.Save(file, ImageFormat.Png);
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.ChangeExtension(file, ".screenshot-error.txt"), ex.Message, Encoding.UTF8);
        }
    }

    private static bool LooksLikeJetDatabase(string file)
    {
        Span<byte> buffer = stackalloc byte[128];
        using var stream = File.OpenRead(file);
        var read = stream.Read(buffer);
        var text = Encoding.ASCII.GetString(buffer[..read]);
        return text.Contains("Standard Jet DB", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, int> SummaryRows(string summaryText)
    {
        var rows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(summaryText);
        if (!doc.RootElement.TryGetProperty("tables", out var tables)) return rows;
        foreach (var table in tables.EnumerateArray())
        {
            var name = table.GetProperty("name").GetString() ?? "";
            var count = table.GetProperty("rows").GetInt32();
            rows[name] = count;
        }
        return rows;
    }

    private static IEnumerable<string> FlattenExpectations(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value)) yield return value;
            yield break;
        }

        if (element.ValueKind != JsonValueKind.Object) yield break;
        foreach (var name in new[] { "name", "note", "contains", "address", "text" })
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                if (!string.IsNullOrWhiteSpace(value)) yield return value;
            }
        }
    }

    private static void Result(string label, bool ok, ref int failed)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {label}");
        if (!ok) failed++;
    }

    private static void Check(string label, bool ok, string detail, ref int failures)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {label}: {detail}");
        if (!ok) failures++;
    }

    private static void Warn(string label, bool ok, string detail, ref int warnings)
    {
        Console.WriteLine($"{(ok ? "PASS" : "WARN")} {label}: {detail}");
        if (!ok) warnings++;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("ERROR: " + message);
        return 1;
    }

    private static string? Opt(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
    }

    private static IEnumerable<string> Opts(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) yield return args[i + 1];
        }
    }

    private static string[] ParseNameList(string[] args, string name)
        => Opts(args, name)
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static int? OptInt(string[] args, string name)
    {
        var value = Opt(args, name);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int ParseInt(string[] args, string name, int fallback)
    {
        var value = Opt(args, name);
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int ParseIntRequired(string[] args, string name)
    {
        var value = Required(args, name);
        if (!int.TryParse(value, out var parsed)) throw new ArgumentException("Invalid integer for " + name);
        return parsed;
    }

    private static long ParseNumber(string[] args, string name, long fallback)
    {
        var value = Opt(args, name);
        return string.IsNullOrWhiteSpace(value) ? fallback : ParseNumberText(value);
    }

    private static long ParseNumberRequired(string[] args, string name)
        => ParseNumberText(Required(args, name));

    private static long ParseNumberText(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(value[2..], 16);
        return long.Parse(value);
    }

    private static string Required(string[] args, string name)
        => Opt(args, name) ?? throw new ArgumentException("Missing " + name);

    private static string RequiredPath(string[] args, string name)
    {
        var path = FullPath(Required(args, name));
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        return path;
    }

    private sealed record WorkflowProjectContext(
        string Project,
        string? Source,
        string? WorkDir,
        string? WorkspaceMarker,
        string ProjectSha256Before,
        string? SourceSha256,
        bool CreatedCopy,
        bool ProfilingCopy,
        string WorkflowName,
        string OperationId,
        DateTimeOffset OperationStartedAt);

    private sealed record WorkflowWorkspaceMarker(
        int SchemaVersion,
        string CreatedBy,
        string Source,
        string SourceSha256,
        string WorkingCopy,
        string InitialWorkingCopySha256,
        string CurrentCandidateSha256,
        string MutationResultsIndex,
        DateTimeOffset CreatedAt);

    private static WorkflowProjectContext PrepareWorkflowProject(string[] args, string workflowName, string outDir)
    {
        SetDialogEvidenceRoot(outDir);
        var sourceOpt = Opt(args, "--source");
        var projectOpt = Opt(args, "--project");
        if (!string.IsNullOrWhiteSpace(sourceOpt) && !string.IsNullOrWhiteSpace(projectOpt))
            throw new ArgumentException("Use either --source or --project, not both.");

        if (!string.IsNullOrWhiteSpace(sourceOpt))
        {
            var source = FullPath(sourceOpt);
            if (!File.Exists(source)) throw new FileNotFoundException(source);
            var workDir = FullPath(Opt(args, "--workdir") ?? Path.Combine(".mcgsctl-work", SafeFile(workflowName) + "-" + Timestamp()));
            EnsureNoReparsePoint(source, "source project");
            EnsureNoReparsePoint(Path.GetDirectoryName(workDir) ?? Environment.CurrentDirectory, "workdir parent");
            EnsureSourceCanBeCopied(source, Has(args, "--allow-copy-open-source"));
            var sourceShaBefore = Sha256(source);
            if (Directory.Exists(workDir) && File.Exists(Path.Combine(workDir, CandidateFileName)))
            {
                if (!Has(args, "--replace-workdir"))
                    throw new InvalidOperationException("Workdir already contains candidate.MCE. Continue with --project <workdir>\\candidate.MCE or pass --replace-workdir.");
                EnsureReplaceWorkdirIsSafe(workDir);
                Directory.Delete(workDir, recursive: true);
            }
            Directory.CreateDirectory(workDir);
            EnsureNoReparsePoint(workDir, "workdir");
            var project = Path.Combine(workDir, CandidateFileName);
            File.Copy(source, project, overwrite: false);
            EnsureNoReparsePoint(project, "working copy");
            var sourceShaAfter = Sha256(source);
            if (!sourceShaBefore.Equals(sourceShaAfter, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(project);
                throw new InvalidOperationException("Source project changed while it was being copied. Working copy was deleted.");
            }

            var workingProjectSha = Sha256(project);
            if (!workingProjectSha.Equals(sourceShaAfter, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(project);
                throw new InvalidOperationException("Working copy SHA256 does not match stable source SHA256. Working copy was deleted.");
            }

            var workspaceMarkerPath = Path.Combine(workDir, "mcgsctl-workspace.json");
            var marker = new WorkflowWorkspaceMarker(
                1,
                "mcgsctl",
                source,
                sourceShaAfter,
                project,
                workingProjectSha,
                workingProjectSha,
                "workflow-results/index.json",
                DateTimeOffset.Now);
            File.WriteAllText(workspaceMarkerPath, JsonSerializer.Serialize(marker, JsonOptions()), Encoding.UTF8);
            var context = new WorkflowProjectContext(project, source, workDir, workspaceMarkerPath, workingProjectSha, sourceShaAfter,
                CreatedCopy: true, ProfilingCopy: false, workflowName, NewOperationId(workDir, workflowName), DateTimeOffset.Now);
            WriteWorkflowAuditStart(outDir, workflowName, context, args);
            return context;
        }

        if (string.IsNullOrWhiteSpace(projectOpt))
            throw new ArgumentException("Write workflows require --source <mce> or --project <copy.mce>.");

        var existingProject = RequiredPath(args, "--project");
        EnsureNoReparsePoint(existingProject, "project");
        var projectSha = Sha256(existingProject);
        var underWork = IsPathUnder(existingProject, FullPath(".mcgsctl-work"));
        var underCodexTmp = IsPathUnder(existingProject, FullPath(".codex_tmp"));
        string? markerPath = null;
        string? workDirFromMarker = null;
        string? sourceFromMarker = null;
        string? sourceSha = null;
        if (underWork)
        {
            markerPath = ValidateWorkflowWorkspaceMarker(existingProject);
            workDirFromMarker = Path.GetDirectoryName(markerPath);
            if (workDirFromMarker != null && File.Exists(markerPath))
            {
                using var markerDoc = JsonDocument.Parse(File.ReadAllText(markerPath, Encoding.UTF8));
                sourceFromMarker = JsonMarkerString(markerDoc.RootElement, "source");
                sourceSha = JsonMarkerString(markerDoc.RootElement, "sourceSha256");
            }
        }
        else if (!underCodexTmp)
        {
            if (!Has(args, "--allow-original"))
            {
                throw new InvalidOperationException(
                    "Refusing to write a project outside .codex_tmp or .mcgsctl-work. Use --source to let mcgsctl create a working copy.");
            }

            var expectedSha = Opt(args, "--expected-project-sha256");
            if (string.IsNullOrWhiteSpace(expectedSha))
            {
                throw new InvalidOperationException(
                    "Direct writes require --expected-project-sha256 with --allow-original.");
            }

            if (!projectSha.Equals(expectedSha.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Project SHA256 mismatch. Expected {expectedSha}, actual {projectSha}.");
            }
        }

        var directContext = new WorkflowProjectContext(existingProject, sourceFromMarker, workDirFromMarker, markerPath, projectSha, sourceSha,
            CreatedCopy: false, ProfilingCopy: underCodexTmp, workflowName,
            NewOperationId(workDirFromMarker ?? outDir, workflowName), DateTimeOffset.Now);
        WriteWorkflowAuditStart(outDir, workflowName, directContext, args);
        return directContext;
    }

    private static void EnsureReplaceWorkdirIsSafe(string workDir)
    {
        var full = Path.GetFullPath(workDir);
        if (!IsPathUnder(full, FullPath(".mcgsctl-work")) && !IsPathUnder(full, FullPath(".codex_tmp")))
            throw new InvalidOperationException("--replace-workdir is only allowed under .mcgsctl-work or .codex_tmp.");
        EnsureNoReparsePoint(full, "replace workdir");
    }

    private static bool IsWorkflowCopyProject(string project)
        => IsPathUnder(project, FullPath(".codex_tmp")) || IsPathUnder(project, FullPath(".mcgsctl-work"));

    private static string ValidateWorkflowWorkspaceMarker(string project)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(project)) ??
                        throw new InvalidOperationException("Project directory was not found.");
        while (true)
        {
            var markerPath = Path.Combine(directory, "mcgsctl-workspace.json");
            if (File.Exists(markerPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(markerPath, Encoding.UTF8));
                var root = doc.RootElement;
                var createdBy = root.TryGetProperty("CreatedBy", out var cb) ? cb.GetString() :
                    root.TryGetProperty("createdBy", out cb) ? cb.GetString() : null;
                var workingCopy = root.TryGetProperty("WorkingCopy", out var wc) ? wc.GetString() :
                    root.TryGetProperty("workingCopy", out wc) ? wc.GetString() : null;
                if (!string.Equals(createdBy, "mcgsctl", StringComparison.Ordinal))
                    throw new InvalidOperationException("Invalid mcgsctl workspace marker: createdBy mismatch.");
                if (string.IsNullOrWhiteSpace(workingCopy) ||
                    !Path.GetFullPath(workingCopy).Equals(Path.GetFullPath(project), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Invalid mcgsctl workspace marker: workingCopy mismatch.");
                return markerPath;
            }

            var parent = Directory.GetParent(directory);
            if (parent == null) break;
            directory = parent.FullName;
        }

        throw new InvalidOperationException("Project requires a matching mcgsctl-workspace.json marker.");
    }

    private static void EnsureSourceCanBeCopied(string source, bool allowOpenSource)
    {
        var directory = Path.GetDirectoryName(source) ?? Environment.CurrentDirectory;
        var stem = Path.GetFileNameWithoutExtension(source);
        var locks = new[]
            {
                Path.Combine(directory, stem + ".ldb"),
                Path.Combine(directory, stem + ".laccdb")
            }
            .Where(File.Exists)
            .ToArray();
        if (locks.Length > 0 && !allowOpenSource)
            throw new InvalidOperationException("Source project appears open because Access lock file(s) exist: " + string.Join(", ", locks));

        if (allowOpenSource) return;
        try
        {
            using var stream = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Source project is locked/open. Close MCGS or use --allow-copy-open-source for profiling only.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException("Source project cannot be opened exclusively for copy safety.", ex);
        }
    }

    private static void EnsureNoReparsePoint(string path, string label)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root)) return;
        var relative = Path.GetRelativePath(root, full);
        var current = root;
        foreach (var part in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = string.IsNullOrEmpty(current) ? part : Path.Combine(current, part);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"{label} contains a junction/symlink/reparse point: {current}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best effort cleanup after a failed safe copy.
        }
    }

    private static bool IsPathUnder(string path, string root)
    {
        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteWorkflowAuditStart(string outDir, string workflowName, WorkflowProjectContext context, string[] args)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "audit-start.json"),
            JsonSerializer.Serialize(new
            {
                workflow = workflowName,
                startedAt = DateTimeOffset.Now,
                context.Project,
                context.Source,
                context.WorkDir,
                context.WorkspaceMarker,
                context.CreatedCopy,
                context.ProfilingCopy,
                context.ProjectSha256Before,
                context.SourceSha256,
                args = RedactArgs(args)
            }, JsonOptions()),
            Encoding.UTF8);
    }

    private static void WriteWorkflowAuditEnd(string outDir, WorkflowProjectContext context, bool saved, bool success)
    {
        File.WriteAllText(Path.Combine(outDir, "audit-end.json"),
            JsonSerializer.Serialize(new
            {
                finishedAt = DateTimeOffset.Now,
                context.Project,
                context.Source,
                context.CreatedCopy,
                context.ProfilingCopy,
                context.WorkspaceMarker,
                saved,
                success,
                projectSha256Before = context.ProjectSha256Before,
                projectSha256After = TrySha256(context.Project, out var afterShaError),
                projectSha256AfterError = afterShaError
            }, JsonOptions()),
            Encoding.UTF8);
    }

    private static string[] RedactArgs(string[] args)
    {
        var result = (string[])args.Clone();
        var includeScriptText = Has(args, "--audit-include-script-text");
        for (var i = 0; i < result.Length - 1; i++)
        {
            if (result[i].Equals("--text", StringComparison.OrdinalIgnoreCase) && !includeScriptText)
            {
                result[i + 1] = $"<sha256:{Sha256Text(result[i + 1])};length:{(result[i + 1] ?? "").Length}>";
            }
            else if (result[i].Contains("password", StringComparison.OrdinalIgnoreCase) ||
                result[i].Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                result[i + 1] = "<redacted>";
            }
        }
        return result;
    }

    private static string Sha256Text(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? ""))).ToLowerInvariant();

    private static bool Has(string[] args, string name)
        => args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static IntPtr OptHwnd(string[] args, string name)
    {
        var text = Opt(args, name);
        return string.IsNullOrWhiteSpace(text) ? IntPtr.Zero : ParseHwnd(text);
    }

    private static IntPtr ParseHwnd(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!long.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var hwndValue) &&
            !long.TryParse(text, out hwndValue))
        {
            throw new ArgumentException("Invalid HWND value.");
        }
        return new IntPtr(hwndValue);
    }

    private static string FullPath(string path)
        => Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));

    private static string EnvOrDefault(string name, string fallback)
        => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? fallback
            : Environment.GetEnvironmentVariable(name)!;

    private static string TryGitCommit()
    {
        try
        {
            var result = ProcessRunner.Run("git", "rev-parse --short HEAD", ToolPaths.FindToolRoot(), 10000);
            return result.ExitCode == 0 ? result.StdOut.Trim() : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string DefaultProject()
    {
        var local = Path.Combine(Environment.CurrentDirectory, "FG2_HMI.MCE");
        return File.Exists(local) ? local : @"E:\myproject\FG2\FG2_HMI.MCE";
    }

    private static string DefaultEditor() => @"E:\MCGSE\Program\McgsSetE.exe";

    private static string Timestamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss");

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    internal static string SafeFile(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var ch in value)
        {
            if (invalid.Contains(ch) || char.IsControl(ch)) sb.Append('_');
            else sb.Append(ch);
        }
        return string.IsNullOrWhiteSpace(sb.ToString()) ? "window" : sb.ToString().Trim();
    }

    private static string FirstLine(string text)
        => text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    private static string Sha256(string file)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string? TrySha256(string? file, out string? error)
    {
        error = null;
        if (file == null || !File.Exists(file)) return null;
        try
        {
            return Sha256(file);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    internal static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };

    private static void WriteJson(object value)
        => Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions()));
}

internal sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

internal sealed class ActionRunner
{
    private int? _pid;
    private IntPtr _hwnd;

    public ActionRunner(int? pid)
    {
        _pid = pid;
        if (pid.HasValue) _hwnd = UiAutomation.FindMainWindow(pid.Value);
    }

    public void Run(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
        var actions = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.GetProperty("actions");

        foreach (var action in actions.EnumerateArray())
        {
            RunAction(action);
        }
    }

    private void RunAction(JsonElement action)
    {
        var kind = GetString(action, "action") ?? GetString(action, "type") ?? throw new ArgumentException("Action missing type.");
        switch (kind.ToLowerInvariant())
        {
            case "open":
                Open(action);
                break;
            case "command":
                UiAutomation.SendCommand(TryGet(action, "hwnd", out _) ? Resolve(action) : MainWindow(), (uint)GetInt(action, "id"));
                break;
            case "find":
                _hwnd = FindChild(action);
                break;
            case "click":
                UiAutomation.ClickButtonByText(Resolve(action), GetString(action, "text") ?? throw new ArgumentException("click needs text"), GetBool(action, "mouse"));
                break;
            case "toolbar":
                RunToolbarAction(action);
                break;
            case "treeview":
                RunTreeViewAction(action);
                break;
            case "listview":
                RunListViewAction(action);
                break;
            case "notify":
                UiAutomation.NotifyControl(Resolve(action), null, GetInt(action, "code"), GetBool(action, "send"), GetInt(action, "delay", 800));
                break;
            case "point":
                UiAutomation.ClickPoint(Resolve(action), GetInt(action, "x"), GetInt(action, "y"),
                    GetBool(action, "right") ? MouseButton.Right : MouseButton.Left,
                    GetBool(action, "double"),
                    GetBool(action, "mouse"));
                break;
            case "drag":
                UiAutomation.DragPoint(Resolve(action), GetInt(action, "x1"), GetInt(action, "y1"), GetInt(action, "x2"), GetInt(action, "y2"), GetBool(action, "mouse"));
                break;
            case "set-text":
            case "settext":
                UiAutomation.SetControlText(Resolve(action), GetTextValue(action), GetBool(action, "paste"));
                break;
            case "keys":
                SendKeys.SendWait(GetString(action, "text") ?? "");
                break;
            case "wait":
                _hwnd = UiAutomation.WaitForWindow(_pid, GetString(action, "title"), GetString(action, "class"), TimeSpan.FromSeconds(GetInt(action, "timeout", 10)));
                if (_hwnd == IntPtr.Zero) throw new TimeoutException("wait action timed out.");
                break;
            case "capture":
                Capture(GetString(action, "out") ?? Path.Combine(".mcgsctl-runs", "script-capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
                break;
            case "sleep":
                Thread.Sleep(GetInt(action, "ms", 1000));
                break;
            case "close":
                UiAutomation.CloseWindow(MainWindow());
                if (_pid.HasValue) UiAutomation.HandleCloseDialogs(_pid.Value, GetBool(action, "save"), TimeSpan.FromSeconds(GetInt(action, "timeout", 12)));
                break;
            default:
                throw new ArgumentException("Unknown action: " + kind);
        }
    }

    private IntPtr FindChild(JsonElement action)
    {
        var root = Resolve(action);
        var className = GetString(action, "class");
        var text = GetString(action, "text");
        var matches = UiAutomation.EnumerateChildren(root)
            .Where(h => string.IsNullOrWhiteSpace(className) || Native.GetClass(h).Contains(className, StringComparison.OrdinalIgnoreCase))
            .Where(h => string.IsNullOrWhiteSpace(text) || Native.GetText(h).Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var index = GetInt(action, "index", 0);
        if (index < 0 || index >= matches.Length) throw new ArgumentOutOfRangeException(nameof(index), "find action did not match a child at index " + index);
        return matches[index];
    }

    private void RunToolbarAction(JsonElement action)
    {
        var hwnd = Resolve(action);
        var buttons = UiAutomation.ToolbarButtons(hwnd);
        if (TryGet(action, "commandId", out var commandId))
        {
            UiAutomation.SendToolbarCommand(hwnd, buttons, b => b.IdCommand == commandId.GetInt32());
            return;
        }
        if (TryGet(action, "commandIndex", out var commandIndex))
        {
            UiAutomation.SendToolbarCommand(hwnd, buttons, b => b.Index == commandIndex.GetInt32());
            return;
        }
        if (TryGet(action, "clickId", out var clickId))
        {
            UiAutomation.ClickToolbarButton(hwnd, buttons, b => b.IdCommand == clickId.GetInt32(), GetBool(action, "mouse"));
            return;
        }
        if (TryGet(action, "clickIndex", out var clickIndex))
        {
            UiAutomation.ClickToolbarButton(hwnd, buttons, b => b.Index == clickIndex.GetInt32(), GetBool(action, "mouse"));
            return;
        }
    }

    private void RunTreeViewAction(JsonElement action)
    {
        var hwnd = Resolve(action);
        if (GetBool(action, "selectRoot"))
        {
            UiAutomation.TreeViewSelect(hwnd, UiAutomation.TreeViewRoot(hwnd));
        }
        if (GetBool(action, "expandRoot"))
        {
            UiAutomation.TreeViewExpand(hwnd, UiAutomation.TreeViewRoot(hwnd));
        }
        if (TryGet(action, "select", out var select) && select.ValueKind == JsonValueKind.String)
        {
            UiAutomation.TreeViewSelect(hwnd, ParseHwndLocal(select.GetString() ?? ""));
        }
        if (TryGet(action, "selectText", out var selectText) && selectText.ValueKind == JsonValueKind.String)
        {
            UiAutomation.TreeViewSelectText(hwnd, selectText.GetString() ?? "");
        }
        if (TryGet(action, "expand", out var expand) && expand.ValueKind == JsonValueKind.String)
        {
            UiAutomation.TreeViewExpand(hwnd, ParseHwndLocal(expand.GetString() ?? ""));
        }
        if (TryGet(action, "expandText", out var expandText) && expandText.ValueKind == JsonValueKind.String)
        {
            UiAutomation.TreeViewExpandText(hwnd, expandText.GetString() ?? "");
        }
    }

    private void RunListViewAction(JsonElement action)
    {
        var hwnd = Resolve(action);
        if (TryGet(action, "selectIndex", out var selectIndex))
        {
            UiAutomation.ListViewSelectIndex(hwnd, selectIndex.GetInt32());
        }
        if (TryGet(action, "doubleIndex", out var doubleIndex))
        {
            UiAutomation.ListViewDoubleClickIndex(hwnd, doubleIndex.GetInt32(), GetBool(action, "mouse"));
        }
    }

    private void Open(JsonElement action)
    {
        var editor = GetString(action, "editor") ?? @"E:\MCGSE\Program\McgsSetE.exe";
        var project = GetString(action, "project") ?? throw new ArgumentException("open action needs project.");
        var process = Process.Start(new ProcessStartInfo(editor, "\"" + Path.GetFullPath(project) + "\"")
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
        }) ?? throw new InvalidOperationException("Failed to start editor.");
        _pid = process.Id;
        _hwnd = WaitMain(process.Id, TimeSpan.FromSeconds(GetInt(action, "timeout", 20)));
    }

    private void Capture(string outDir)
    {
        outDir = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outDir);
        var windows = _pid.HasValue ? UiAutomation.TopWindowsForPid(_pid.Value).ToArray() : UiAutomation.AllMcgsWindows().ToArray();
        var infos = new List<WindowInfo>();
        for (var i = 0; i < windows.Length; i++)
        {
            var info = WindowInfo.FromHandle(windows[i]);
            infos.Add(info);
            var prefix = $"{i:D2}-{Program.SafeFile(info.ClassName)}-{Program.SafeFile(info.Text)}";
            if (prefix.Length > 90) prefix = prefix[..90];
            File.WriteAllLines(Path.Combine(outDir, prefix + ".tree.txt"), UiAutomation.WindowTreeLines(windows[i]), Encoding.UTF8);
            Program.TryScreenshot(windows[i], Path.Combine(outDir, prefix + ".png"));
        }
        File.WriteAllText(Path.Combine(outDir, "windows.json"), JsonSerializer.Serialize(infos, Program.JsonOptions()), Encoding.UTF8);
    }

    private IntPtr MainWindow()
    {
        if (_pid.HasValue)
        {
            var hwnd = UiAutomation.FindMainWindow(_pid.Value);
            if (hwnd != IntPtr.Zero) return hwnd;
        }
        if (_hwnd != IntPtr.Zero) return _hwnd;
        if (UiAutomation.TryFindLatestMainWindow(out var latest)) return latest;
        throw new InvalidOperationException("No MCGS main window is available.");
    }

    private IntPtr Resolve(JsonElement action)
    {
        var target = GetString(action, "target") ?? GetString(action, "scope") ?? GetString(action, "root");
        if (target != null && target.Equals("main", StringComparison.OrdinalIgnoreCase)) return MainWindow();
        if (target != null && target.Equals("current", StringComparison.OrdinalIgnoreCase)) return _hwnd != IntPtr.Zero ? _hwnd : MainWindow();
        var hwnd = GetString(action, "hwnd");
        if (!string.IsNullOrWhiteSpace(hwnd)) return ParseHwndLocal(hwnd);
        return _hwnd != IntPtr.Zero ? _hwnd : MainWindow();
    }

    private static IntPtr WaitMain(int pid, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var hwnd = UiAutomation.FindMainWindow(pid);
            if (hwnd != IntPtr.Zero) return hwnd;
            Thread.Sleep(150);
        }
        throw new TimeoutException("Timed out waiting for main window.");
    }

    private static string GetTextValue(JsonElement action)
    {
        if (TryGet(action, "file", out var file)) return File.ReadAllText(Path.GetFullPath(file.GetString() ?? ""), Encoding.UTF8);
        return GetString(action, "text") ?? "";
    }

    private static string? GetString(JsonElement element, string name)
        => TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int GetInt(JsonElement element, string name, int fallback = int.MinValue)
    {
        if (TryGet(element, name, out var value) && value.TryGetInt32(out var parsed)) return parsed;
        if (fallback != int.MinValue) return fallback;
        throw new ArgumentException("Action missing integer " + name);
    }

    private static bool GetBool(JsonElement element, string name)
        => TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static IntPtr ParseHwndLocal(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!long.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value) &&
            !long.TryParse(text, out value)) throw new ArgumentException("Invalid HWND value.");
        return new IntPtr(value);
    }
}

internal static class ProcessRunner
{
    public static ProcessResult TryRun(string fileName, string arguments, string workingDirectory, int timeoutMs)
    {
        try
        {
            return Run(fileName, arguments, workingDirectory, timeoutMs);
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, "", ex.Message);
        }
    }

    public static ProcessResult Run(string fileName, string arguments, string workingDirectory, int timeoutMs)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };
        process.Start();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"{fileName} timed out.");
        }
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}

internal static class ToolPaths
{
    public static string FindToolRoot()
    {
        foreach (var candidate in CandidateRoots())
        {
            if (File.Exists(Path.Combine(candidate, "java", "MceExport.java")) &&
                Directory.Exists(Path.Combine(candidate, "lib")))
            {
                return candidate;
            }
        }
        throw new DirectoryNotFoundException("Cannot locate tools/mcgsctl root.");
    }

    public static IEnumerable<string> JackcessJars(string toolRoot)
    {
        yield return Path.Combine(toolRoot, "lib", "jackcess-4.0.10.jar");
        yield return Path.Combine(toolRoot, "lib", "commons-lang3-3.18.0.jar");
        yield return Path.Combine(toolRoot, "lib", "commons-logging-1.2.jar");
    }

    public static string DefaultInspectPath()
        => @"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\inspect.exe";

    private static IEnumerable<string> CandidateRoots()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            yield return dir.FullName;
            dir = dir.Parent;
        }
        yield return Path.Combine(Environment.CurrentDirectory, "tools", "mcgsctl");
        yield return Environment.CurrentDirectory;
    }
}

internal static class MceExporter
{
    public static void Export(string project, string outDir)
    {
        var toolRoot = ToolPaths.FindToolRoot();
        EnsureCompiled(toolRoot);
        Directory.CreateDirectory(outDir);
        var exportProject = Path.Combine(outDir, "_source.MCE");
        File.Copy(project, exportProject, overwrite: true);
        var cache = JavaCache(toolRoot);
        var classPath = string.Join(";", ToolPaths.JackcessJars(toolRoot).Concat(new[] { cache }).Select(Quote));
        var args = $"-cp {classPath} MceExport {Quote(exportProject)} {Quote(outDir)}";
        var result = ProcessRunner.Run("java", args, toolRoot, 120000);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("MCE export failed: " + result.StdErr + result.StdOut);
    }

    public static void BlobDiff(string before, string after, string outDir)
    {
        var toolRoot = ToolPaths.FindToolRoot();
        EnsureCompiled(toolRoot);
        Directory.CreateDirectory(outDir);
        var cache = JavaCache(toolRoot);
        var classPath = string.Join(";", ToolPaths.JackcessJars(toolRoot).Concat(new[] { cache }).Select(Quote));
        var args = $"-cp {classPath} MceBlobDiff {Quote(before)} {Quote(after)} {Quote(outDir)}";
        var result = ProcessRunner.Run("java", args, toolRoot, 120000);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("MCE blob diff failed: " + result.StdErr + result.StdOut);
    }

    public static void EnsureCompiled(string toolRoot)
    {
        var source = Path.Combine(toolRoot, "java", "MceExport.java");
        var cache = JavaCache(toolRoot);
        Directory.CreateDirectory(cache);

        var classPath = string.Join(";", ToolPaths.JackcessJars(toolRoot).Select(Quote));
        var args = $"-encoding UTF-8 -cp {classPath} -d {Quote(cache)} {Quote(source)}";
        var result = ProcessRunner.Run("javac", args, toolRoot, 120000);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Java helper compile failed: " + result.StdErr + result.StdOut);
    }

    private static string JavaCache(string toolRoot)
        => Path.Combine(toolRoot, ".mcgsctl-cache", "java");

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}

internal sealed record MenuEntry(string Path, uint? Id);

internal enum MouseButton
{
    Left,
    Right
}

internal sealed record PopupMenuItem(int Index, uint? Id, string Text);

internal sealed record PopupMenuInfo(WindowInfo Window, PopupMenuItem[] Items);

internal sealed record ControlItem(int Index, string Text);

internal sealed record ToolbarButtonInfo(
    int Index,
    int IdCommand,
    int Image,
    byte State,
    byte Style,
    bool Enabled,
    bool Hidden,
    int Left,
    int Top,
    int Right,
    int Bottom,
    string Text);

internal sealed record TreeViewItemInfo(
    string Handle,
    int Level,
    string Text,
    bool HasChildren,
    Rect Rect);

internal sealed record ListViewItemInfo(
    int Index,
    string[] Texts,
    Rect Rect);

internal sealed record TabItemInfo(
    int Index,
    string Text,
    Rect Rect);

internal sealed record MdiInfo(
    string MainHandle,
    string MdiClientHandle,
    string ActiveMdiHandle,
    string ActiveMdiClass,
    string ActiveMdiText,
    string ActiveViewHandle,
    string ActiveViewClass,
    string ActiveViewText);

internal sealed record GuiFocusInfo(
    uint ThreadId,
    string ForegroundHandle,
    string ActiveHandle,
    string FocusHandle,
    string CaptureHandle,
    string MenuOwnerHandle,
    string MoveSizeHandle,
    string CaretHandle,
    Rect CaretRect);

internal sealed record ModuleInfo(
    string Name,
    string Path,
    string BaseAddress,
    string EndAddress,
    uint Size);

internal sealed record AddressModuleInfo(
    string Address,
    string ModuleName,
    string ModulePath,
    string Offset,
    bool InKnownModule);

internal sealed record WindowProcInfo(
    string Handle,
    string ClassName,
    string Text,
    int ProcessId,
    AddressModuleInfo WindowProc,
    AddressModuleInfo ClassProc,
    AddressModuleInfo DialogProcOffset4,
    AddressModuleInfo DialogProcOffset8);

internal sealed record PeExportInfo(
    string File,
    string Machine,
    string Bitness,
    string? ExportName,
    string ExportDirectoryRva,
    string ExportDirectorySize,
    PeExportEntry[] Exports);

internal sealed record PeExportEntry(
    int Ordinal,
    string? Name,
    string Rva,
    string? Forwarder);

internal sealed record BinaryStringInfo(
    string Offset,
    string Encoding,
    string Text);

internal sealed record WindowInfo(string Handle, string ClassName, string Text, int Left, int Top, int Width, int Height)
{
    public static WindowInfo FromHandle(IntPtr hwnd)
    {
        var rect = UiAutomation.GetWindowRect(hwnd);
        return new WindowInfo(
            "0x" + hwnd.ToInt64().ToString("X"),
            Native.GetClass(hwnd),
            Native.GetText(hwnd),
            rect.Left,
            rect.Top,
            rect.Width,
            rect.Height);
    }
}

internal readonly record struct Rect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

internal sealed class RemoteMemory : IDisposable
{
    private readonly IntPtr _process;
    public IntPtr Address { get; }
    public int Size { get; }

    private RemoteMemory(IntPtr process, IntPtr address, int size)
    {
        _process = process;
        Address = address;
        Size = size;
    }

    public static RemoteMemory AllocateForWindow(IntPtr hwnd, int size)
    {
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        var process = Native.OpenProcess(Native.ProcessVmOperation | Native.ProcessVmRead | Native.ProcessVmWrite | Native.ProcessQueryInformation, false, pid);
        if (process == IntPtr.Zero) throw new InvalidOperationException("OpenProcess failed.");
        var address = Native.VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)size, Native.MemCommit | Native.MemReserve, Native.PageReadWrite);
        if (address == IntPtr.Zero)
        {
            Native.CloseHandle(process);
            throw new InvalidOperationException("VirtualAllocEx failed.");
        }
        return new RemoteMemory(process, address, size);
    }

    public void Write(byte[] data)
    {
        if (data.Length > Size) throw new ArgumentOutOfRangeException(nameof(data));
        if (!Native.WriteProcessMemory(_process, Address, data, data.Length, out _))
            throw new InvalidOperationException("WriteProcessMemory failed.");
    }

    public byte[] Read(int length)
    {
        if (length > Size) throw new ArgumentOutOfRangeException(nameof(length));
        var data = new byte[length];
        if (!Native.ReadProcessMemory(_process, Address, data, data.Length, out _))
            throw new InvalidOperationException("ReadProcessMemory failed.");
        return data;
    }

    public void Dispose()
    {
        if (Address != IntPtr.Zero) Native.VirtualFreeEx(_process, Address, UIntPtr.Zero, Native.MemRelease);
        if (_process != IntPtr.Zero) Native.CloseHandle(_process);
    }
}

internal static class ProcessDiagnostics
{
    public static ModuleInfo[] GetModules(int pid)
    {
        var snapshot = Native.CreateToolhelp32Snapshot(Native.TH32CS_SNAPMODULE | Native.TH32CS_SNAPMODULE32, (uint)pid);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            throw new InvalidOperationException("CreateToolhelp32Snapshot failed.");

        try
        {
            var modules = new List<ModuleInfo>();
            var entry = new Native.ModuleEntry32 { dwSize = (uint)Marshal.SizeOf<Native.ModuleEntry32>() };
            if (!Native.Module32First(snapshot, ref entry)) return modules.ToArray();
            do
            {
                var baseAddress = ToUInt64(entry.modBaseAddr);
                modules.Add(new ModuleInfo(
                    entry.szModule,
                    entry.szExePath,
                    FormatAddress(baseAddress),
                    FormatAddress(baseAddress + entry.modBaseSize),
                    entry.modBaseSize));
                entry.dwSize = (uint)Marshal.SizeOf<Native.ModuleEntry32>();
            }
            while (Native.Module32Next(snapshot, ref entry));

            return modules
                .GroupBy(m => m.BaseAddress + "|" + m.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(m => ParseAddress(m.BaseAddress))
                .ToArray();
        }
        finally
        {
            Native.CloseHandle(snapshot);
        }
    }

    public static WindowProcInfo GetWindowProcInfo(IntPtr hwnd, int pid, ModuleInfo[] modules)
    {
        return new WindowProcInfo(
            "0x" + hwnd.ToInt64().ToString("X"),
            Native.GetClass(hwnd),
            Native.GetText(hwnd),
            pid,
            MapAddress(Native.GetWindowLongPtr(hwnd, Native.GWLP_WNDPROC), modules),
            MapAddress(Native.GetClassLongPtr(hwnd, Native.GCLP_WNDPROC), modules),
            MapAddress(Native.GetWindowLongPtr(hwnd, 4), modules),
            MapAddress(Native.GetWindowLongPtr(hwnd, 8), modules));
    }

    private static AddressModuleInfo MapAddress(IntPtr address, ModuleInfo[] modules)
    {
        var value = ToUInt64(address);
        if (value == 0)
            return new AddressModuleInfo("0x0", "", "", "0x0", false);

        foreach (var module in modules)
        {
            var start = ParseAddress(module.BaseAddress);
            var end = ParseAddress(module.EndAddress);
            if (value >= start && value < end)
            {
                return new AddressModuleInfo(
                    FormatAddress(value),
                    module.Name,
                    module.Path,
                    FormatAddress(value - start),
                    true);
            }
        }

        return new AddressModuleInfo(FormatAddress(value), "", "", "0x0", false);
    }

    private static ulong ToUInt64(IntPtr value)
        => unchecked((ulong)value.ToInt64());

    private static ulong ParseAddress(string value)
        => Convert.ToUInt64(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value, 16);

    private static string FormatAddress(ulong value)
        => "0x" + value.ToString("X");
}

internal static class PeInspector
{
    public static PeExportInfo ReadExports(string file)
    {
        using var stream = File.OpenRead(file);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        if (reader.ReadUInt16() != 0x5A4D) throw new InvalidDataException("Missing MZ header.");
        stream.Position = 0x3C;
        var peOffset = reader.ReadUInt32();
        stream.Position = peOffset;
        if (reader.ReadUInt32() != 0x00004550) throw new InvalidDataException("Missing PE header.");

        var machine = reader.ReadUInt16();
        var sectionCount = reader.ReadUInt16();
        reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadUInt32();
        var optionalHeaderSize = reader.ReadUInt16();
        reader.ReadUInt16();

        var optionalHeaderOffset = stream.Position;
        var magic = ReadUInt16(reader, optionalHeaderOffset);
        var isPe32Plus = magic == 0x20B;
        if (magic != 0x10B && magic != 0x20B)
            throw new InvalidDataException("Unsupported PE optional header magic.");

        var dataDirectoryOffset = optionalHeaderOffset + (isPe32Plus ? 112 : 96);
        var exportRva = ReadUInt32(reader, dataDirectoryOffset);
        var exportSize = ReadUInt32(reader, dataDirectoryOffset + 4);

        var sectionOffset = optionalHeaderOffset + optionalHeaderSize;
        var sections = ReadSections(reader, sectionOffset, sectionCount);
        if (exportRva == 0)
        {
            return new PeExportInfo(
                Path.GetFullPath(file),
                MachineName(machine),
                isPe32Plus ? "PE32+" : "PE32",
                null,
                "0x0",
                "0x0",
                Array.Empty<PeExportEntry>());
        }

        var exportOffset = RvaToOffset(exportRva, sections);
        stream.Position = exportOffset;
        reader.ReadUInt32();
        reader.ReadUInt32();
        reader.ReadUInt16();
        reader.ReadUInt16();
        var exportNameRva = reader.ReadUInt32();
        var ordinalBase = reader.ReadUInt32();
        var functionCount = reader.ReadUInt32();
        var nameCount = reader.ReadUInt32();
        var functionsRva = reader.ReadUInt32();
        var namesRva = reader.ReadUInt32();
        var ordinalsRva = reader.ReadUInt32();

        var functionRvas = new uint[functionCount];
        for (var i = 0; i < functionRvas.Length; i++)
            functionRvas[i] = ReadUInt32(reader, RvaToOffset(functionsRva + (uint)(i * 4), sections));

        var namesByIndex = new Dictionary<int, string>();
        for (var i = 0; i < nameCount; i++)
        {
            var nameRva = ReadUInt32(reader, RvaToOffset(namesRva + (uint)(i * 4), sections));
            var ordinalIndex = ReadUInt16(reader, RvaToOffset(ordinalsRva + (uint)(i * 2), sections));
            namesByIndex[ordinalIndex] = ReadAsciiZ(reader, RvaToOffset(nameRva, sections));
        }

        var exports = new List<PeExportEntry>();
        for (var i = 0; i < functionRvas.Length; i++)
        {
            var rva = functionRvas[i];
            if (rva == 0) continue;
            var forwarder = rva >= exportRva && rva < exportRva + exportSize
                ? ReadAsciiZ(reader, RvaToOffset(rva, sections))
                : null;
            namesByIndex.TryGetValue(i, out var name);
            exports.Add(new PeExportEntry((int)ordinalBase + i, name, FormatRva(rva), forwarder));
        }

        return new PeExportInfo(
            Path.GetFullPath(file),
            MachineName(machine),
            isPe32Plus ? "PE32+" : "PE32",
            exportNameRva == 0 ? null : ReadAsciiZ(reader, RvaToOffset(exportNameRva, sections)),
            FormatRva(exportRva),
            FormatRva(exportSize),
            exports.OrderBy(e => e.Ordinal).ToArray());
    }

    private static PeSection[] ReadSections(BinaryReader reader, long offset, int count)
    {
        var sections = new PeSection[count];
        for (var i = 0; i < count; i++)
        {
            reader.BaseStream.Position = offset + i * 40L;
            var name = Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0');
            var virtualSize = reader.ReadUInt32();
            var virtualAddress = reader.ReadUInt32();
            var rawSize = reader.ReadUInt32();
            var rawOffset = reader.ReadUInt32();
            reader.BaseStream.Position += 16;
            sections[i] = new PeSection(name, virtualAddress, virtualSize, rawOffset, rawSize);
        }
        return sections;
    }

    private static long RvaToOffset(uint rva, PeSection[] sections)
    {
        foreach (var section in sections)
        {
            var size = Math.Max(section.VirtualSize, section.RawSize);
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + size)
                return section.RawOffset + (rva - section.VirtualAddress);
        }
        return rva;
    }

    private static ushort ReadUInt16(BinaryReader reader, long offset)
    {
        reader.BaseStream.Position = offset;
        return reader.ReadUInt16();
    }

    private static uint ReadUInt32(BinaryReader reader, long offset)
    {
        reader.BaseStream.Position = offset;
        return reader.ReadUInt32();
    }

    private static string ReadAsciiZ(BinaryReader reader, long offset)
    {
        reader.BaseStream.Position = offset;
        var bytes = new List<byte>();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var b = reader.ReadByte();
            if (b == 0) break;
            bytes.Add(b);
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static string MachineName(ushort machine)
        => machine switch
        {
            0x014C => "x86",
            0x8664 => "x64",
            0x01C0 => "ARM",
            0xAA64 => "ARM64",
            _ => "0x" + machine.ToString("X4")
        };

    private static string FormatRva(uint rva)
        => "0x" + rva.ToString("X");

    private sealed record PeSection(string Name, uint VirtualAddress, uint VirtualSize, uint RawOffset, uint RawSize);
}

internal static class BinaryStrings
{
    public static BinaryStringInfo[] Extract(string file, int minLength, string encoding)
    {
        if (minLength < 1) throw new ArgumentOutOfRangeException(nameof(minLength));
        if (encoding is not ("ansi" or "unicode" or "both"))
            throw new ArgumentException("Unknown --encoding. Use ansi, unicode, or both.");

        var data = File.ReadAllBytes(file);
        var results = new List<BinaryStringInfo>();
        if (encoding is "ansi" or "both") ExtractAnsi(data, minLength, results);
        if (encoding is "unicode" or "both") ExtractUnicode(data, minLength, results);
        return results
            .Where(s => LooksUseful(s.Text))
            .OrderBy(s => Convert.ToInt64(s.Offset[2..], 16))
            .ThenBy(s => s.Encoding)
            .ToArray();
    }

    private static void ExtractAnsi(byte[] data, int minLength, List<BinaryStringInfo> results)
    {
        var i = 0;
        while (i < data.Length)
        {
            while (i < data.Length && !IsAnsiTextByte(data[i])) i++;
            var start = i;
            while (i < data.Length && IsAnsiTextByte(data[i])) i++;
            var length = i - start;
            if (length >= minLength)
            {
                var text = DecodeAnsi(data.AsSpan(start, length).ToArray()).Trim();
                if (text.Length >= minLength)
                    results.Add(new BinaryStringInfo(FormatOffset(start), "ansi", text));
            }
        }
    }

    private static void ExtractUnicode(byte[] data, int minLength, List<BinaryStringInfo> results)
    {
        for (var alignment = 0; alignment < 2; alignment++)
        {
            var i = alignment;
            while (i + 1 < data.Length)
            {
                while (i + 1 < data.Length && !IsUnicodeTextChar(ReadChar(data, i))) i += 2;
                var start = i;
                while (i + 1 < data.Length && IsUnicodeTextChar(ReadChar(data, i))) i += 2;
                var charLength = (i - start) / 2;
                if (charLength >= minLength)
                {
                    var text = Encoding.Unicode.GetString(data, start, i - start).Trim();
                    if (text.Length >= minLength)
                        results.Add(new BinaryStringInfo(FormatOffset(start), "unicode", text));
                }
            }
        }
    }

    private static bool IsAnsiTextByte(byte value)
        => value == 0x09 || value >= 0x20;

    private static char ReadChar(byte[] data, int offset)
        => (char)(data[offset] | (data[offset + 1] << 8));

    private static bool IsUnicodeTextChar(char value)
        => value != '\0' && !char.IsControl(value) && !char.IsSurrogate(value);

    private static bool LooksUseful(string text)
        => text.Any(ch => char.IsLetterOrDigit(ch) || IsCjk(ch));

    private static bool IsCjk(char ch)
        => ch >= 0x3400 && ch <= 0x9FFF;

    private static string DecodeAnsi(byte[] raw)
    {
        var ptr = Marshal.AllocHGlobal(raw.Length + 1);
        try
        {
            Marshal.Copy(raw, 0, ptr, raw.Length);
            Marshal.WriteByte(ptr, raw.Length, 0);
            return Marshal.PtrToStringAnsi(ptr) ?? "";
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static string FormatOffset(int offset)
        => "0x" + offset.ToString("X");
}

internal static class UiAutomation
{
    public static bool TryFindLatestMainWindow(out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        var processes = Process.GetProcessesByName("McgsSetE")
            .OrderByDescending(SafeStartTime)
            .ToArray();
        foreach (var process in processes)
        {
            hwnd = FindMainWindow(process.Id);
            if (hwnd != IntPtr.Zero) return true;
        }
        return false;
    }

    public static IEnumerable<IntPtr> AllMcgsWindows()
    {
        return Process.GetProcessesByName("McgsSetE")
            .OrderByDescending(SafeStartTime)
            .SelectMany(process => TopWindowsForPid(process.Id));
    }

    public static IntPtr FindMainWindow(int pid)
    {
        var windows = new List<IntPtr>();
        Native.EnumWindows((hwnd, _) =>
        {
            if (!Native.IsWindowVisible(hwnd)) return true;
            if (GetWindowProcessId(hwnd) != pid) return true;
            var text = Native.GetText(hwnd);
            if (string.IsNullOrWhiteSpace(text)) return true;
            windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        return windows
            .OrderByDescending(w => Native.GetClass(w) != "#32770")
            .ThenByDescending(w => Native.GetText(w).Contains("MCGS", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(w => Native.GetText(w).Contains("组态", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(w => GetWindowRect(w).Width * GetWindowRect(w).Height)
            .FirstOrDefault();
    }

    public static IntPtr WaitForWindow(int? pid, string? title, string? className, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var windows = pid.HasValue ? TopWindowsForPid(pid.Value) : AllMcgsWindows();
            var hwnd = windows.FirstOrDefault(h =>
                (string.IsNullOrWhiteSpace(title) || Native.GetText(h).Contains(title, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(className) || Native.GetClass(h).Contains(className, StringComparison.OrdinalIgnoreCase)));
            if (hwnd != IntPtr.Zero) return hwnd;
            Thread.Sleep(150);
        }
        return IntPtr.Zero;
    }

    public static int GetWindowProcessId(IntPtr hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    public static IEnumerable<string> WindowTreeLines(IntPtr root)
    {
        yield return FormatWindow(root, 0);
        foreach (var child in EnumerateChildren(root))
        {
            yield return FormatWindow(child, 1);
        }
    }

    public static IReadOnlyList<MenuEntry> GetMenus(IntPtr hwnd)
    {
        var menu = Native.GetMenu(hwnd);
        var entries = new List<MenuEntry>();
        if (menu == IntPtr.Zero) return entries;
        ReadMenu(menu, "", entries);
        return entries;
    }

    public static MdiInfo GetMdiInfo(IntPtr main)
    {
        var mdiClient = FindDescendant(main, h => Native.GetClass(h) == "MDIClient");
        var active = mdiClient == IntPtr.Zero
            ? IntPtr.Zero
            : Native.SendMessage(mdiClient, Native.WM_MDIGETACTIVE, IntPtr.Zero, IntPtr.Zero);
        var view = active;
        if (view != IntPtr.Zero && !Native.GetClass(view).StartsWith("AfxFrameOrView", StringComparison.OrdinalIgnoreCase))
            view = FindDescendant(active, h => Native.GetClass(h).StartsWith("AfxFrameOrView", StringComparison.OrdinalIgnoreCase));
        if (view == IntPtr.Zero)
            view = FindDescendant(main, h => Native.GetClass(h).StartsWith("AfxFrameOrView", StringComparison.OrdinalIgnoreCase));

        return new MdiInfo(
            FormatHandle(main),
            FormatHandle(mdiClient),
            FormatHandle(active),
            active == IntPtr.Zero ? "" : Native.GetClass(active),
            active == IntPtr.Zero ? "" : Native.GetText(active),
            FormatHandle(view),
            view == IntPtr.Zero ? "" : Native.GetClass(view),
            view == IntPtr.Zero ? "" : Native.GetText(view));
    }

    public static GuiFocusInfo GetGuiInfo(IntPtr hwnd)
    {
        var threadId = Native.GetWindowThreadProcessId(hwnd, out _);
        var info = new Native.GuiThreadInfo { cbSize = Marshal.SizeOf<Native.GuiThreadInfo>() };
        Native.GetGUIThreadInfo(threadId, ref info);
        return new GuiFocusInfo(
            threadId,
            FormatHandle(Native.GetForegroundWindow()),
            FormatHandle(info.hwndActive),
            FormatHandle(info.hwndFocus),
            FormatHandle(info.hwndCapture),
            FormatHandle(info.hwndMenuOwner),
            FormatHandle(info.hwndMoveSize),
            FormatHandle(info.hwndCaret),
            new Rect(info.rcCaret.Left, info.rcCaret.Top, info.rcCaret.Right, info.rcCaret.Bottom));
    }

    public static void ActivateForInput(IntPtr hwnd)
    {
        var root = Native.GetAncestor(hwnd, Native.GA_ROOT);
        if (root == IntPtr.Zero) root = hwnd;
        var targetThread = Native.GetWindowThreadProcessId(hwnd, out _);
        var currentThread = Native.GetCurrentThreadId();
        var attached = targetThread != currentThread && Native.AttachThreadInput(currentThread, targetThread, true);
        try
        {
            Native.SetForegroundWindow(root);
            Native.BringWindowToTop(root);
            Native.SetActiveWindow(hwnd);
            Native.SetFocus(hwnd);
        }
        finally
        {
            if (attached) Native.AttachThreadInput(currentThread, targetThread, false);
        }
    }

    public static void SendCommand(IntPtr hwnd, uint id, bool send = false, int hiword = 0)
    {
        ActivateForInput(hwnd);
        var wParam = Native.MakeWParam((int)id, hiword);
        if (send)
        {
            if (Native.SendMessageTimeout(hwnd, Native.WM_COMMAND, wParam, IntPtr.Zero,
                    Native.SMTO_ABORTIFHUNG, 3000, out _) == IntPtr.Zero)
                throw new TimeoutException($"WM_COMMAND {id} timed out or failed.");
        }
        else
        {
            Native.PostMessage(hwnd, Native.WM_COMMAND, wParam, IntPtr.Zero);
        }
    }

    public static bool ClickButtonByText(IntPtr root, string text, bool mouse)
    {
        var buttons = EnumerateChildren(root)
            .Where(h => Native.GetClass(h).Contains("Button", StringComparison.OrdinalIgnoreCase))
            .Select(h => new { Handle = h, Text = Native.GetText(h) })
            .ToList();

        var exact = buttons.FirstOrDefault(b => b.Text.Equals(text, StringComparison.OrdinalIgnoreCase));
        var match = exact ?? buttons.FirstOrDefault(b => b.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (match == null) return false;

        Native.SetForegroundWindow(root);
        if (mouse)
        {
            var rect = GetWindowRect(match.Handle);
            Native.SetForegroundWindow(Native.GetAncestor(match.Handle, Native.GA_ROOT));
            Native.SetCursorPos(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            Thread.Sleep(80);
            Native.MouseEvent(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            Native.MouseEvent(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }
        else
        {
            Native.PostMessage(match.Handle, Native.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }
        return true;
    }

    public static void CloseWindow(IntPtr hwnd)
    {
        Native.SetForegroundWindow(hwnd);
        Native.PostMessage(hwnd, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    public static bool HandleCloseDialogs(int pid, bool saveIntent, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var main = FindMainWindow(pid);
            if (main == IntPtr.Zero) return true;

            foreach (var dialog in TopWindowsForPid(pid).Where(h => Native.GetClass(h) == "#32770"))
            {
                var title = Native.GetText(dialog);
                var dialogText = title + "\n" + string.Join("\n", EnumerateChildren(dialog).Select(Native.GetText));
                var buttons = EnumerateChildren(dialog)
                    .Where(h => Native.GetClass(h).Contains("Button", StringComparison.OrdinalIgnoreCase))
                    .Select(h => new { Handle = h, Text = Native.GetText(h) })
                    .ToList();

                string[] preferred;
                if (dialogText.Contains("退出组态环境", StringComparison.OrdinalIgnoreCase) ||
                    dialogText.Contains("退出组态", StringComparison.OrdinalIgnoreCase))
                {
                    preferred = new[] { "是(&Y)", "是", "Yes", "确定" };
                }
                else if (dialogText.Contains("保存", StringComparison.OrdinalIgnoreCase))
                {
                    preferred = saveIntent
                        ? new[] { "是(&Y)", "是", "Yes", "确定" }
                        : new[] { "否(&N)", "否", "No" };
                }
                else
                {
                    preferred = new[] { "是(&Y)", "是", "Yes", "确定", "否(&N)", "否", "No" };
                }

                foreach (var label in preferred)
                {
                    var button = buttons.FirstOrDefault(b => b.Text.Equals(label, StringComparison.OrdinalIgnoreCase));
                    if (button == null) continue;
                    Native.SendMessage(button.Handle, Native.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    Thread.Sleep(500);
                    break;
                }
            }

            Thread.Sleep(250);
        }
        return FindMainWindow(pid) == IntPtr.Zero;
    }

    public static Rect GetWindowRect(IntPtr hwnd)
    {
        Native.GetWindowRect(hwnd, out var rect);
        return new Rect(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public static IEnumerable<IntPtr> TopWindowsForPid(int pid)
    {
        var windows = new List<IntPtr>();
        Native.EnumWindows((hwnd, _) =>
        {
            if (GetWindowProcessId(hwnd) == pid && Native.IsWindowVisible(hwnd)) windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static IEnumerable<IntPtr> EnumerateChildren(IntPtr root)
    {
        var children = new List<IntPtr>();
        Native.EnumChildWindows(root, (hwnd, _) =>
        {
            children.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return children;
    }

    public static void SetControlText(IntPtr hwnd, string text, bool paste = false)
    {
        if (paste)
        {
            PasteControlText(hwnd, text);
            return;
        }

        Native.SetWindowText(hwnd, text);
        NotifyParent(hwnd, Native.EN_UPDATE);
        NotifyParent(hwnd, Native.EN_CHANGE);
        NotifyParent(hwnd, Native.EN_KILLFOCUS);
    }

    private static void PasteControlText(IntPtr hwnd, string text)
    {
        Native.SetForegroundWindow(Native.GetAncestor(hwnd, Native.GA_ROOT));
        Native.SetFocus(hwnd);
        var rect = GetWindowRect(hwnd);
        ClickPoint(hwnd, Math.Min(8, Math.Max(1, rect.Width / 2)), Math.Max(1, rect.Height / 2), MouseButton.Left, false, mouse: true);
        Thread.Sleep(80);
        Clipboard.SetText(text ?? "");
        Native.SendMessage(hwnd, Native.EM_SETSEL, IntPtr.Zero, new IntPtr(-1));
        Thread.Sleep(50);
        Native.SendMessage(hwnd, Native.WM_CLEAR, IntPtr.Zero, IntPtr.Zero);
        Thread.Sleep(50);
        Native.SendMessage(hwnd, Native.WM_PASTE, IntPtr.Zero, IntPtr.Zero);
        Thread.Sleep(120);
        NotifyParent(hwnd, Native.EN_UPDATE);
        NotifyParent(hwnd, Native.EN_CHANGE);
        NotifyParent(hwnd, Native.EN_KILLFOCUS);
    }

    public static void NotifyControl(IntPtr hwnd, IntPtr? parentOverride, int code, bool send, int delayMs)
    {
        var parent = parentOverride.GetValueOrDefault();
        if (parent == IntPtr.Zero) parent = Native.GetParent(hwnd);
        if (parent == IntPtr.Zero) throw new InvalidOperationException("Control parent was not found.");

        var id = Native.GetDlgCtrlID(hwnd);
        var pointerSize = Native.TargetPointerSize(hwnd);
        var headerSize = pointerSize == 4 ? 12 : 24;
        using var memory = RemoteMemory.AllocateForWindow(parent, headerSize);
        var raw = new byte[headerSize];
        WritePointer(raw, 0, hwnd, pointerSize);
        WritePointer(raw, pointerSize, new IntPtr(id), pointerSize);
        WriteInt32(raw, pointerSize * 2, code);
        memory.Write(raw);

        Native.SetForegroundWindow(Native.GetAncestor(hwnd, Native.GA_ROOT));
        if (send)
        {
            Native.SendMessage(parent, Native.WM_NOTIFY, new IntPtr(id), memory.Address);
        }
        else
        {
            Native.PostMessage(parent, Native.WM_NOTIFY, new IntPtr(id), memory.Address);
            Thread.Sleep(Math.Max(50, delayMs));
        }
    }

    public static ToolbarButtonInfo[] ToolbarButtons(IntPtr hwnd)
    {
        var count = (int)Native.SendMessage(hwnd, Native.TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt64();
        if (count < 0 || count > 512) throw new InvalidOperationException($"Unexpected toolbar button count: {count}");

        var buttons = new List<ToolbarButtonInfo>();
        var pointerSize = Native.TargetPointerSize(hwnd);
        var buttonSize = pointerSize == 4 ? 20 : 32;
        using var buttonMemory = RemoteMemory.AllocateForWindow(hwnd, buttonSize);
        using var rectMemory = RemoteMemory.AllocateForWindow(hwnd, 16);

        for (var i = 0; i < count; i++)
        {
            if (Native.SendMessage(hwnd, Native.TB_GETBUTTON, new IntPtr(i), buttonMemory.Address) == IntPtr.Zero)
                continue;

            var rawButton = buttonMemory.Read(buttonSize);
            var iBitmap = BitConverter.ToInt32(rawButton, 0);
            var idCommand = BitConverter.ToInt32(rawButton, 4);
            var state = rawButton[8];
            var style = rawButton[9];

            var left = 0;
            var top = 0;
            var right = 0;
            var bottom = 0;
            if (Native.SendMessage(hwnd, Native.TB_GETITEMRECT, new IntPtr(i), rectMemory.Address) != IntPtr.Zero)
            {
                var rawRect = rectMemory.Read(16);
                left = BitConverter.ToInt32(rawRect, 0);
                top = BitConverter.ToInt32(rawRect, 4);
                right = BitConverter.ToInt32(rawRect, 8);
                bottom = BitConverter.ToInt32(rawRect, 12);
            }

            buttons.Add(new ToolbarButtonInfo(
                i,
                idCommand,
                iBitmap,
                state,
                style,
                (state & Native.TBSTATE_ENABLED) != 0,
                (state & Native.TBSTATE_HIDDEN) != 0,
                left,
                top,
                right,
                bottom,
                ToolbarButtonText(hwnd, idCommand)));
        }

        return buttons.ToArray();
    }

    public static void ClickToolbarButton(IntPtr hwnd, IEnumerable<ToolbarButtonInfo> buttons, Func<ToolbarButtonInfo, bool> predicate, bool mouse)
    {
        var button = buttons.FirstOrDefault(predicate)
            ?? throw new ArgumentException("Toolbar button was not found.");
        if (button.Hidden) throw new InvalidOperationException("Toolbar button is hidden.");
        var x = button.Left + Math.Max(1, button.Right - button.Left) / 2;
        var y = button.Top + Math.Max(1, button.Bottom - button.Top) / 2;
        ClickPoint(hwnd, x, y, MouseButton.Left, false, mouse);
    }

    public static void SendToolbarCommand(IntPtr hwnd, IEnumerable<ToolbarButtonInfo> buttons, Func<ToolbarButtonInfo, bool> predicate)
    {
        var button = buttons.FirstOrDefault(predicate)
            ?? throw new ArgumentException("Toolbar button was not found.");
        if (button.IdCommand <= 0) throw new InvalidOperationException("Toolbar button does not have a command id.");
        var parent = Native.GetParent(hwnd);
        if (parent == IntPtr.Zero) parent = hwnd;
        Native.SetForegroundWindow(parent);
        Native.PostMessage(parent, Native.WM_COMMAND, new IntPtr(button.IdCommand), hwnd);
    }

    public static TreeViewItemInfo[] TreeViewItems(IntPtr hwnd)
    {
        var items = new List<TreeViewItemInfo>();
        var root = TreeViewRoot(hwnd);
        var visited = new HashSet<IntPtr>();
        WalkTreeView(hwnd, root, 0, items, visited);
        return items.ToArray();
    }

    public static IntPtr TreeViewRoot(IntPtr hwnd)
        => Native.SendMessage(hwnd, Native.TVM_GETNEXTITEM, new IntPtr(Native.TVGN_ROOT), IntPtr.Zero);

    public static IntPtr TreeViewCaret(IntPtr hwnd)
        => Native.SendMessage(hwnd, Native.TVM_GETNEXTITEM, new IntPtr(Native.TVGN_CARET), IntPtr.Zero);

    public static void TreeViewSelect(IntPtr hwnd, IntPtr item)
    {
        ActivateForInput(hwnd);
        Native.SendMessage(hwnd, Native.TVM_ENSUREVISIBLE, IntPtr.Zero, item);
        Native.SendMessage(hwnd, Native.TVM_SELECTITEM, new IntPtr(Native.TVGN_CARET), item);
        Thread.Sleep(100);
        var caret = TreeViewCaret(hwnd);
        if (caret != item)
            throw new InvalidOperationException($"TreeView selection did not stick. caret=0x{caret.ToInt64():X} expected=0x{item.ToInt64():X}");
    }

    public static void TreeViewExpand(IntPtr hwnd, IntPtr item)
    {
        ActivateForInput(hwnd);
        Native.SendMessage(hwnd, Native.TVM_EXPAND, new IntPtr(Native.TVE_EXPAND), item);
    }

    public static void TreeViewSelectText(IntPtr hwnd, string text)
    {
        TreeViewSelect(hwnd, ParseHwnd(TreeViewFindItem(hwnd, text).Handle));
    }

    public static void TreeViewExpandText(IntPtr hwnd, string text)
    {
        TreeViewExpand(hwnd, ParseHwnd(TreeViewFindItem(hwnd, text).Handle));
    }

    public static TreeViewItemInfo TreeViewFindItem(IntPtr hwnd, string text)
        => TreeViewItems(hwnd).FirstOrDefault(i => i.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException("Tree item not found: " + text);

    public static void TreeViewNotifySelectionChanged(IntPtr hwnd, IntPtr item, bool unicode, bool send)
    {
        var parent = Native.GetParent(hwnd);
        if (parent == IntPtr.Zero) throw new InvalidOperationException("TreeView parent was not found.");

        var id = Native.GetDlgCtrlID(hwnd);
        var pointerSize = Native.TargetPointerSize(hwnd);
        var headerSize = pointerSize == 4 ? 12 : 24;
        var itemSize = pointerSize == 4 ? 40 : 56;
        var actionOffset = headerSize;
        var oldItemOffset = actionOffset + 4 + (pointerSize == 8 ? 4 : 0);
        var newItemOffset = oldItemOffset + itemSize;
        var pointOffset = newItemOffset + itemSize;
        var notifySize = pointOffset + 8;

        using var memory = RemoteMemory.AllocateForWindow(parent, notifySize);
        var raw = new byte[notifySize];
        WritePointer(raw, 0, hwnd, pointerSize);
        WritePointer(raw, pointerSize, new IntPtr(id), pointerSize);
        WriteInt32(raw, pointerSize * 2, unicode ? Native.TVN_SELCHANGEDW : Native.TVN_SELCHANGEDA);
        WriteInt32(raw, actionOffset, Native.TVC_BYMOUSE);
        WriteTreeItemForNotify(raw, newItemOffset, pointerSize, item);
        memory.Write(raw);

        ActivateForInput(hwnd);
        if (send)
            Native.SendMessage(parent, Native.WM_NOTIFY, new IntPtr(id), memory.Address);
        else
        {
            Native.PostMessage(parent, Native.WM_NOTIFY, new IntPtr(id), memory.Address);
            Thread.Sleep(300);
        }
    }

    public static Rect TreeViewItemRect(IntPtr hwnd, IntPtr item)
    {
        using var memory = RemoteMemory.AllocateForWindow(hwnd, 16);
        var raw = new byte[16];
        WriteInt32(raw, 0, item.ToInt64());
        memory.Write(raw);
        if (Native.SendMessage(hwnd, Native.TVM_GETITEMRECT, IntPtr.Zero, memory.Address) == IntPtr.Zero)
            return new Rect(0, 0, 0, 0);

        var rect = memory.Read(16);
        return new Rect(
            BitConverter.ToInt32(rect, 0),
            BitConverter.ToInt32(rect, 4),
            BitConverter.ToInt32(rect, 8),
            BitConverter.ToInt32(rect, 12));
    }

    public static string TreeViewItemText(IntPtr hwnd, IntPtr item)
    {
        var pointerSize = Native.TargetPointerSize(hwnd);
        var itemSize = pointerSize == 4 ? 40 : 56;
        const int textBytes = 1024;
        using var itemMemory = RemoteMemory.AllocateForWindow(hwnd, itemSize);
        using var textMemory = RemoteMemory.AllocateForWindow(hwnd, textBytes);
        var raw = new byte[itemSize];
        WriteUInt32(raw, 0, Native.TVIF_TEXT | Native.TVIF_CHILDREN);
        WritePointer(raw, pointerSize == 4 ? 4 : 8, item, pointerSize);
        WritePointer(raw, pointerSize == 4 ? 16 : 24, textMemory.Address, pointerSize);
        WriteInt32(raw, pointerSize == 4 ? 20 : 32, textBytes - 1);
        itemMemory.Write(raw);

        Native.SendMessage(hwnd, Native.TVM_GETITEMA, IntPtr.Zero, itemMemory.Address);
        return DecodeAnsiBytes(textMemory.Read(textBytes));
    }

    public static ListViewItemInfo[] ListViewItems(IntPtr hwnd)
    {
        var count = Native.SendMessage(hwnd, Native.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
        if (count < 0 || count > 10000) throw new InvalidOperationException($"Unexpected listview item count: {count}");
        var items = new List<ListViewItemInfo>();
        for (var i = 0; i < count; i++)
        {
            var texts = new List<string>();
            for (var subItem = 0; subItem < 8; subItem++)
            {
                var text = ListViewItemText(hwnd, i, subItem);
                if (subItem > 1 && string.IsNullOrWhiteSpace(text)) break;
                texts.Add(text);
            }
            items.Add(new ListViewItemInfo(i, texts.ToArray(), ListViewItemRect(hwnd, i)));
        }
        return items.ToArray();
    }

    public static void ListViewSelectIndex(IntPtr hwnd, int index)
    {
        var pointerSize = Native.TargetPointerSize(hwnd);
        var itemSize = pointerSize == 4 ? 60 : 88;
        using var itemMemory = RemoteMemory.AllocateForWindow(hwnd, itemSize);
        var raw = new byte[itemSize];
        var state = Native.LVIS_SELECTED | Native.LVIS_FOCUSED;
        WriteUInt32(raw, 12, state);
        WriteUInt32(raw, 16, state);
        itemMemory.Write(raw);
        Native.SendMessage(hwnd, Native.LVM_ENSUREVISIBLE, new IntPtr(index), IntPtr.Zero);
        Native.SendMessage(hwnd, Native.LVM_SETITEMSTATE, new IntPtr(index), itemMemory.Address);
        Native.SetFocus(hwnd);
    }

    public static void ListViewSelectText(IntPtr hwnd, string text)
        => ListViewSelectIndex(hwnd, ListViewFindIndex(hwnd, text));

    public static void ListViewDoubleClickIndex(IntPtr hwnd, int index, bool mouse)
    {
        ListViewSelectIndex(hwnd, index);
        Thread.Sleep(100);
        var rect = ListViewItemRect(hwnd, index);
        var x = rect.Left + Math.Min(80, Math.Max(8, rect.Width / 3));
        var y = rect.Top + Math.Max(1, rect.Height / 2);
        ClickPoint(hwnd, x, y, MouseButton.Left, true, mouse);
    }

    public static void ListViewDoubleClickText(IntPtr hwnd, string text, bool mouse)
        => ListViewDoubleClickIndex(hwnd, ListViewFindIndex(hwnd, text), mouse);

    private static int ListViewFindIndex(IntPtr hwnd, string text)
    {
        var item = ListViewItems(hwnd).FirstOrDefault(i =>
            i.Texts.Any(t => t.Contains(text, StringComparison.OrdinalIgnoreCase)));
        if (item == null) throw new ArgumentException("ListView item not found: " + text);
        return item.Index;
    }

    private static string ListViewItemText(IntPtr hwnd, int index, int subItem)
    {
        var ansi = ListViewItemText(hwnd, index, subItem, unicode: false);
        if (!string.IsNullOrWhiteSpace(ansi)) return ansi;
        return ListViewItemText(hwnd, index, subItem, unicode: true);
    }

    private static string ListViewItemText(IntPtr hwnd, int index, int subItem, bool unicode)
    {
        var pointerSize = Native.TargetPointerSize(hwnd);
        var itemSize = pointerSize == 4 ? 60 : 88;
        const int textBytes = 1024;
        using var itemMemory = RemoteMemory.AllocateForWindow(hwnd, itemSize);
        using var textMemory = RemoteMemory.AllocateForWindow(hwnd, textBytes);
        var raw = new byte[itemSize];
        WriteUInt32(raw, 0, Native.LVIF_TEXT);
        WriteInt32(raw, 4, index);
        WriteInt32(raw, 8, subItem);
        WritePointer(raw, pointerSize == 4 ? 20 : 24, textMemory.Address, pointerSize);
        WriteInt32(raw, pointerSize == 4 ? 24 : 32, unicode ? (textBytes / 2) - 1 : textBytes - 1);
        itemMemory.Write(raw);
        Native.SendMessage(hwnd, unicode ? Native.LVM_GETITEMTEXTW : Native.LVM_GETITEMTEXTA, new IntPtr(index), itemMemory.Address);
        var bytes = textMemory.Read(textBytes);
        return unicode ? DecodeUtf16Bytes(bytes) : DecodeAnsiBytes(bytes);
    }

    private static Rect ListViewItemRect(IntPtr hwnd, int index)
    {
        using var memory = RemoteMemory.AllocateForWindow(hwnd, 16);
        var raw = new byte[16];
        WriteInt32(raw, 0, Native.LVIR_BOUNDS);
        memory.Write(raw);
        if (Native.SendMessage(hwnd, Native.LVM_GETITEMRECT, new IntPtr(index), memory.Address) == IntPtr.Zero)
            return new Rect(0, 0, 0, 0);
        var rect = memory.Read(16);
        return new Rect(
            BitConverter.ToInt32(rect, 0),
            BitConverter.ToInt32(rect, 4),
            BitConverter.ToInt32(rect, 8),
            BitConverter.ToInt32(rect, 12));
    }

    public static ControlItem[] ComboItems(IntPtr hwnd)
    {
        var count = Native.SendMessage(hwnd, Native.CB_GETCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
        var items = new List<ControlItem>();
        for (var i = 0; i < count; i++)
        {
            items.Add(new ControlItem(i, GetComboText(hwnd, i)));
        }
        return items.ToArray();
    }

    public static int ComboCurrentIndex(IntPtr hwnd)
        => Native.SendMessage(hwnd, Native.CB_GETCURSEL, IntPtr.Zero, IntPtr.Zero).ToInt32();

    public static void ComboSelectIndex(IntPtr hwnd, int index)
    {
        Native.SendMessage(hwnd, Native.CB_SETCURSEL, new IntPtr(index), IntPtr.Zero);
        NotifyParent(hwnd, Native.CBN_SELCHANGE);
    }

    public static void ComboSelectText(IntPtr hwnd, string text)
    {
        var items = ComboItems(hwnd);
        var item = items.FirstOrDefault(i => i.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (item == null) throw new ArgumentException("Combo item not found: " + text);
        ComboSelectIndex(hwnd, item.Index);
    }

    public static ControlItem[] ListBoxItems(IntPtr hwnd)
    {
        var count = Native.SendMessage(hwnd, Native.LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
        var items = new List<ControlItem>();
        for (var i = 0; i < count; i++)
        {
            items.Add(new ControlItem(i, GetListBoxText(hwnd, i)));
        }
        return items.ToArray();
    }

    public static int ListBoxCurrentIndex(IntPtr hwnd)
        => Native.SendMessage(hwnd, Native.LB_GETCURSEL, IntPtr.Zero, IntPtr.Zero).ToInt32();

    public static void ListBoxSelectIndex(IntPtr hwnd, int index)
    {
        Native.SendMessage(hwnd, Native.LB_SETCURSEL, new IntPtr(index), IntPtr.Zero);
        NotifyParent(hwnd, Native.LBN_SELCHANGE);
    }

    public static void ListBoxSelectText(IntPtr hwnd, string text)
    {
        var item = ListBoxItems(hwnd).FirstOrDefault(i => i.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("List item not found: " + text);
        ListBoxSelectIndex(hwnd, item.Index);
    }

    public static TabItemInfo[] TabItems(IntPtr hwnd)
    {
        var count = Native.SendMessage(hwnd, Native.TCM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
        if (count < 0 || count > 1000) throw new InvalidOperationException($"Unexpected tab item count: {count}");
        var items = new List<TabItemInfo>();
        for (var i = 0; i < count; i++)
        {
            items.Add(new TabItemInfo(i, TabItemText(hwnd, i), TabItemRect(hwnd, i)));
        }
        return items.ToArray();
    }

    public static void TabSelectIndex(IntPtr hwnd, int index, bool mouse = false)
    {
        if (mouse)
        {
            var rect = TabItemRect(hwnd, index);
            if (rect.Width <= 0 || rect.Height <= 0) throw new InvalidOperationException("Tab item rectangle was not found.");
            ClickPoint(hwnd, rect.Left + rect.Width / 2, rect.Top + rect.Height / 2, MouseButton.Left, false, mouse: true);
            Thread.Sleep(150);
            return;
        }

        NotifyControl(hwnd, null, Native.TCN_SELCHANGING, send: true, delayMs: 0);
        Native.SendMessage(hwnd, Native.TCM_SETCURSEL, new IntPtr(index), IntPtr.Zero);
        NotifyControl(hwnd, null, Native.TCN_SELCHANGE, send: true, delayMs: 0);
    }

    public static void TabSelectText(IntPtr hwnd, string text, bool mouse = false)
    {
        var item = TabItems(hwnd).FirstOrDefault(i => i.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Tab item not found: " + text);
        TabSelectIndex(hwnd, item.Index, mouse);
    }

    private static Rect TabItemRect(IntPtr hwnd, int index)
    {
        using var memory = RemoteMemory.AllocateForWindow(hwnd, 16);
        if (Native.SendMessage(hwnd, Native.TCM_GETITEMRECT, new IntPtr(index), memory.Address) == IntPtr.Zero)
            return new Rect(0, 0, 0, 0);
        var raw = memory.Read(16);
        return new Rect(
            BitConverter.ToInt32(raw, 0),
            BitConverter.ToInt32(raw, 4),
            BitConverter.ToInt32(raw, 8),
            BitConverter.ToInt32(raw, 12));
    }

    private static string TabItemText(IntPtr hwnd, int index)
    {
        const int textBytes = 512;
        var pointerSize = Native.TargetPointerSize(hwnd);
        var itemSize = pointerSize == 4 ? 28 : 40;
        using var itemMemory = RemoteMemory.AllocateForWindow(hwnd, itemSize);
        using var textMemory = RemoteMemory.AllocateForWindow(hwnd, textBytes);
        var raw = new byte[itemSize];
        WriteUInt32(raw, 0, Native.TCIF_TEXT);
        WritePointer(raw, pointerSize == 4 ? 12 : 16, textMemory.Address, pointerSize);
        WriteInt32(raw, pointerSize == 4 ? 16 : 24, textBytes - 1);
        itemMemory.Write(raw);

        var ok = Native.SendMessage(hwnd, Native.TCM_GETITEMA, new IntPtr(index), itemMemory.Address);
        if (ok == IntPtr.Zero) return "";
        return DecodeAnsiBytes(textMemory.Read(textBytes));
    }

    public static int ButtonGetCheck(IntPtr hwnd)
        => Native.SendMessage(hwnd, Native.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero).ToInt32();

    public static void ButtonSetCheck(IntPtr hwnd, bool isChecked)
        => Native.SendMessage(hwnd, Native.BM_SETCHECK, new IntPtr(isChecked ? 1 : 0), IntPtr.Zero);

    public static void ButtonToggleCheck(IntPtr hwnd)
        => ButtonSetCheck(hwnd, ButtonGetCheck(hwnd) == 0);

    public static void ClickPoint(IntPtr hwnd, int x, int y, MouseButton button, bool doubleClick, bool mouse)
    {
        Native.SetForegroundWindow(mouse ? Native.GetAncestor(hwnd, Native.GA_ROOT) : hwnd);
        var repeat = doubleClick ? 2 : 1;
        if (mouse)
        {
            var rect = GetWindowRect(hwnd);
            Native.SetCursorPos(rect.Left + x, rect.Top + y);
            Thread.Sleep(80);
            for (var i = 0; i < repeat; i++)
            {
                Native.MouseEvent(button == MouseButton.Right ? Native.MOUSEEVENTF_RIGHTDOWN : Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(60);
                Native.MouseEvent(button == MouseButton.Right ? Native.MOUSEEVENTF_RIGHTUP : Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(80);
            }
            return;
        }

        var down = button == MouseButton.Right ? Native.WM_RBUTTONDOWN : Native.WM_LBUTTONDOWN;
        var up = button == MouseButton.Right ? Native.WM_RBUTTONUP : Native.WM_LBUTTONUP;
        var wParam = button == MouseButton.Right ? new IntPtr(Native.MK_RBUTTON) : new IntPtr(Native.MK_LBUTTON);
        var lParam = Native.MakeLParam(x, y);
        if (doubleClick)
        {
            Native.PostMessage(hwnd, down, wParam, lParam);
            Thread.Sleep(60);
            Native.PostMessage(hwnd, up, IntPtr.Zero, lParam);
            Thread.Sleep(90);
            Native.PostMessage(hwnd, button == MouseButton.Right ? Native.WM_RBUTTONDBLCLK : Native.WM_LBUTTONDBLCLK, wParam, lParam);
            Thread.Sleep(60);
            Native.PostMessage(hwnd, up, IntPtr.Zero, lParam);
            return;
        }

        for (var i = 0; i < repeat; i++)
        {
            Native.PostMessage(hwnd, down, wParam, lParam);
            Thread.Sleep(60);
            Native.PostMessage(hwnd, up, IntPtr.Zero, lParam);
            Thread.Sleep(90);
        }
    }

    public static void DragPoint(IntPtr hwnd, int x1, int y1, int x2, int y2, bool mouse)
    {
        Native.SetForegroundWindow(mouse ? Native.GetAncestor(hwnd, Native.GA_ROOT) : hwnd);
        if (mouse)
        {
            var rect = GetWindowRect(hwnd);
            Native.SetCursorPos(rect.Left + x1, rect.Top + y1);
            Thread.Sleep(80);
            Native.MouseEvent(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(80);
            Native.SetCursorPos(rect.Left + x2, rect.Top + y2);
            Native.MouseEvent(Native.MOUSEEVENTF_MOVE, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(120);
            Native.MouseEvent(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            return;
        }

        var start = Native.MakeLParam(x1, y1);
        var end = Native.MakeLParam(x2, y2);
        Native.PostMessage(hwnd, Native.WM_LBUTTONDOWN, new IntPtr(Native.MK_LBUTTON), start);
        Thread.Sleep(80);
        Native.PostMessage(hwnd, Native.WM_MOUSEMOVE, new IntPtr(Native.MK_LBUTTON), end);
        Thread.Sleep(120);
        Native.PostMessage(hwnd, Native.WM_LBUTTONUP, IntPtr.Zero, end);
    }

    private static string ToolbarButtonText(IntPtr hwnd, int idCommand)
    {
        if (idCommand <= 0) return "";
        using var textMemory = RemoteMemory.AllocateForWindow(hwnd, 1024);
        var length = (int)Native.SendMessage(hwnd, Native.TB_GETBUTTONTEXTW, new IntPtr(idCommand), textMemory.Address).ToInt64();
        if (length > 0)
        {
            var raw = textMemory.Read(Math.Min(1024, (length + 1) * 2));
            return Encoding.Unicode.GetString(raw).TrimEnd('\0');
        }

        length = (int)Native.SendMessage(hwnd, Native.TB_GETBUTTONTEXTA, new IntPtr(idCommand), textMemory.Address).ToInt64();
        if (length > 0)
        {
            return DecodeAnsiBytes(textMemory.Read(Math.Min(1024, length + 1)));
        }

        return "";
    }

    private static void WalkTreeView(IntPtr hwnd, IntPtr item, int level, List<TreeViewItemInfo> items, HashSet<IntPtr> visited)
    {
        var guard = 0;
        while (item != IntPtr.Zero && guard++ < 512)
        {
            if (!visited.Add(item)) return;
            var child = Native.SendMessage(hwnd, Native.TVM_GETNEXTITEM, new IntPtr(Native.TVGN_CHILD), item);
            items.Add(new TreeViewItemInfo(
                "0x" + item.ToInt64().ToString("X"),
                level,
                TreeViewItemText(hwnd, item),
                child != IntPtr.Zero,
                TreeViewItemRect(hwnd, item)));

            if (child != IntPtr.Zero) WalkTreeView(hwnd, child, level + 1, items, visited);
            item = Native.SendMessage(hwnd, Native.TVM_GETNEXTITEM, new IntPtr(Native.TVGN_NEXT), item);
        }
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
    }

    private static void WriteInt32(byte[] buffer, int offset, long value)
    {
        var bytes = BitConverter.GetBytes(unchecked((int)value));
        Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
    }

    private static void WriteTreeItemForNotify(byte[] buffer, int offset, int pointerSize, IntPtr item)
    {
        WriteUInt32(buffer, offset, Native.TVIF_HANDLE | Native.TVIF_STATE);
        WritePointer(buffer, offset + (pointerSize == 4 ? 4 : 8), item, pointerSize);
        WriteUInt32(buffer, offset + (pointerSize == 4 ? 8 : 16), Native.TVIS_SELECTED);
        WriteUInt32(buffer, offset + (pointerSize == 4 ? 12 : 20), Native.TVIS_SELECTED);
    }

    private static void WritePointer(byte[] buffer, int offset, IntPtr value, int pointerSize)
    {
        var raw = value.ToInt64();
        var bytes = pointerSize == 4
            ? BitConverter.GetBytes(unchecked((int)raw))
            : BitConverter.GetBytes(raw);
        Buffer.BlockCopy(bytes, 0, buffer, offset, pointerSize);
    }

    private static string DecodeAnsiBytes(byte[] raw)
    {
        var length = Array.IndexOf(raw, (byte)0);
        if (length < 0) length = raw.Length;
        if (length == 0) return "";
        var ptr = Marshal.AllocHGlobal(length + 1);
        try
        {
            Marshal.Copy(raw, 0, ptr, length);
            Marshal.WriteByte(ptr, length, 0);
            return Marshal.PtrToStringAnsi(ptr) ?? "";
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static string DecodeUtf16Bytes(byte[] raw)
    {
        var length = 0;
        for (var i = 0; i + 1 < raw.Length; i += 2)
        {
            if (raw[i] == 0 && raw[i + 1] == 0) break;
            length += 2;
        }
        if (length == 0) return "";
        return Encoding.Unicode.GetString(raw, 0, length);
    }

    private static string GetComboText(IntPtr hwnd, int index)
        => GetTextByIndex(hwnd, Native.CB_GETLBTEXTLEN, Native.CB_GETLBTEXT, index, forceAnsi: true);

    private static string GetListBoxText(IntPtr hwnd, int index)
        => GetTextByIndex(hwnd, Native.LB_GETTEXTLEN, Native.LB_GETTEXT, index, forceAnsi: true);

    private static string GetTextByIndex(IntPtr hwnd, uint lenMsg, uint textMsg, int index, bool forceAnsi = false)
    {
        var length = Math.Max(0, Native.SendMessage(hwnd, lenMsg, new IntPtr(index), IntPtr.Zero).ToInt32());
        var bytes = Math.Max(512, (length + 4) * 2);
        var buffer = Marshal.AllocHGlobal(bytes);
        try
        {
            for (var i = 0; i < bytes; i++) Marshal.WriteByte(buffer, i, 0);
            Native.SendMessage(hwnd, textMsg, new IntPtr(index), buffer);
            var raw = new byte[bytes];
            Marshal.Copy(buffer, raw, 0, bytes);
            if (forceAnsi) return DecodeAnsiBytes(raw);
            return LooksLikeUnicode(raw)
                ? (Marshal.PtrToStringUni(buffer) ?? "")
                : DecodeAnsiBytes(raw);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool LooksLikeUnicode(byte[] raw)
    {
        var sample = Math.Min(raw.Length, 128);
        var oddZeros = 0;
        var oddSeen = 0;
        for (var i = 1; i < sample; i += 2)
        {
            oddSeen++;
            if (raw[i] == 0) oddZeros++;
        }
        return oddSeen > 0 && oddZeros > oddSeen / 2;
    }

    private static void NotifyParent(IntPtr hwnd, int code)
    {
        var parent = Native.GetParent(hwnd);
        if (parent == IntPtr.Zero) return;
        var id = Native.GetDlgCtrlID(hwnd);
        var wParam = new IntPtr((code << 16) | (id & 0xFFFF));
        Native.PostMessage(parent, Native.WM_COMMAND, wParam, hwnd);
    }

    public static IEnumerable<PopupMenuInfo> GetPopupMenus(int? pid)
    {
        var windows = new List<IntPtr>();
        Native.EnumWindows((hwnd, _) =>
        {
            if (!Native.IsWindowVisible(hwnd)) return true;
            if (Native.GetClass(hwnd) != "#32768") return true;
            if (pid.HasValue && GetWindowProcessId(hwnd) != pid.Value) return true;
            windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        foreach (var hwnd in windows)
        {
            var menu = Native.SendMessage(hwnd, Native.MN_GETHMENU, IntPtr.Zero, IntPtr.Zero);
            var items = new List<PopupMenuItem>();
            var count = Native.GetMenuItemCount(menu);
            for (var i = 0; i < count; i++)
            {
                var id = Native.GetMenuItemID(menu, i);
                var text = Native.GetMenuStringByPosition(menu, i).Replace("&", "");
                items.Add(new PopupMenuItem(i, id == uint.MaxValue ? null : id, text));
            }
            yield return new PopupMenuInfo(WindowInfo.FromHandle(hwnd), items.ToArray());
        }
    }

    public static void OpenContextMenu(IntPtr hwnd, int x, int y)
    {
        var rect = GetWindowRect(hwnd);
        var screenX = rect.Left + x;
        var screenY = rect.Top + y;
        ActivateForInput(hwnd);
        Native.SendMessage(hwnd, Native.WM_CONTEXTMENU, hwnd, Native.MakeLParam(screenX, screenY));
    }

    public static void ChoosePopupItem(PopupMenuInfo popup, int? index, int? id, string? text, bool exact, bool mouse)
    {
        var item = index.HasValue
            ? popup.Items.FirstOrDefault(i => i.Index == index.Value)
            : id.HasValue
                ? popup.Items.FirstOrDefault(i => i.Id == (uint)id.Value)
                : popup.Items.FirstOrDefault(i => i.Text.Contains(text ?? "", StringComparison.OrdinalIgnoreCase));
        if (item == null) throw new ArgumentException("Popup item not found.");

        var hwnd = ParseHwnd(popup.Window.Handle);
        if (exact)
        {
            var menu = Native.SendMessage(hwnd, Native.MN_GETHMENU, IntPtr.Zero, IntPtr.Zero);
            if (menu != IntPtr.Zero && Native.GetMenuItemRect(IntPtr.Zero, menu, (uint)item.Index, out var itemRect))
            {
                ClickScreenPoint(itemRect.Left + (itemRect.Right - itemRect.Left) / 2,
                    itemRect.Top + (itemRect.Bottom - itemRect.Top) / 2);
                return;
            }
        }

        var y = EstimatePopupItemCenterY(popup.Window.Height, popup.Items.Length, item.Index);
        ClickPoint(hwnd, Math.Max(10, popup.Window.Width / 2), y, MouseButton.Left, false, mouse);
    }

    private static void ClickScreenPoint(int x, int y)
    {
        Native.SetCursorPos(x, y);
        Thread.Sleep(80);
        Native.MouseEvent(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        Native.MouseEvent(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    private static int EstimatePopupItemCenterY(int height, int itemCount, int index)
    {
        if (itemCount <= 0) return Math.Max(1, height / 2);
        var row = Math.Max(16, height / itemCount);
        return Math.Min(height - 4, row * index + row / 2);
    }

    private static IntPtr ParseHwnd(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!long.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value) &&
            !long.TryParse(text, out value)) throw new ArgumentException("Invalid HWND value.");
        return new IntPtr(value);
    }

    private static string FormatHandle(IntPtr hwnd)
        => "0x" + hwnd.ToInt64().ToString("X");

    private static IntPtr FindDescendant(IntPtr root, Func<IntPtr, bool> predicate)
        => EnumerateChildren(root).FirstOrDefault(predicate);

    private static string FormatWindow(IntPtr hwnd, int level)
    {
        var rect = GetWindowRect(hwnd);
        return $"{new string(' ', level * 2)}0x{hwnd.ToInt64():X} class={Native.GetClass(hwnd)} text={Native.GetText(hwnd)} rect={rect.Left},{rect.Top},{rect.Width},{rect.Height}";
    }

    private static void ReadMenu(IntPtr menu, string prefix, List<MenuEntry> entries)
    {
        var count = Native.GetMenuItemCount(menu);
        for (var i = 0; i < count; i++)
        {
            var text = Native.GetMenuStringByPosition(menu, i).Replace("&", "");
            if (string.IsNullOrWhiteSpace(text)) text = "(separator)";
            var path = string.IsNullOrWhiteSpace(prefix) ? text : prefix + " > " + text;
            var subMenu = Native.GetSubMenu(menu, i);
            if (subMenu != IntPtr.Zero)
            {
                entries.Add(new MenuEntry(path, null));
                ReadMenu(subMenu, path, entries);
            }
            else
            {
                var id = Native.GetMenuItemID(menu, i);
                entries.Add(new MenuEntry(path, id == uint.MaxValue ? null : id));
            }
        }
    }

    private static DateTime SafeStartTime(Process process)
    {
        try { return process.StartTime; }
        catch { return DateTime.MinValue; }
    }
}

internal static class Native
{
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_NOTIFY = 0x004E;
    public const uint WM_GETOBJECT = 0x003D;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_GETTEXT = 0x000D;
    public const uint WM_SETTEXT = 0x000C;
    public const uint WM_CLEAR = 0x0303;
    public const uint WM_PASTE = 0x0302;
    public const uint EM_SETSEL = 0x00B1;
    public const int EN_KILLFOCUS = 0x0200;
    public const int EN_CHANGE = 0x0300;
    public const int EN_UPDATE = 0x0400;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_LBUTTONDBLCLK = 0x0203;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_RBUTTONDBLCLK = 0x0206;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_MDIGETACTIVE = 0x0229;
    public const uint WM_MDIACTIVATE = 0x0222;
    public const int MK_LBUTTON = 0x0001;
    public const int MK_RBUTTON = 0x0002;
    public const uint MN_GETHMENU = 0x01E1;
    public const uint BM_CLICK = 0x00F5;
    public const uint BM_GETCHECK = 0x00F0;
    public const uint BM_SETCHECK = 0x00F1;
    public const uint CB_GETCOUNT = 0x0146;
    public const uint CB_GETCURSEL = 0x0147;
    public const uint CB_GETLBTEXT = 0x0148;
    public const uint CB_GETLBTEXTLEN = 0x0149;
    public const uint CB_SETCURSEL = 0x014E;
    public const int CBN_SELCHANGE = 1;
    public const uint LB_GETCOUNT = 0x018B;
    public const uint LB_GETCURSEL = 0x0188;
    public const uint LB_GETTEXT = 0x0189;
    public const uint LB_GETTEXTLEN = 0x018A;
    public const uint LB_SETCURSEL = 0x0186;
    public const int LBN_SELCHANGE = 1;
    public const uint TCM_GETITEMCOUNT = 0x1304;
    public const uint TCM_GETITEMA = 0x1305;
    public const uint TCM_SETCURSEL = 0x130C;
    public const uint TCM_GETITEMRECT = 0x130A;
    public const uint TCIF_TEXT = 0x0001;
    public const int TCN_SELCHANGING = -552;
    public const int TCN_SELCHANGE = -551;
    public const uint TB_BUTTONCOUNT = 0x0418;
    public const uint TB_GETBUTTON = 0x0417;
    public const uint TB_GETITEMRECT = 0x041D;
    public const uint TB_GETBUTTONTEXTA = 0x042D;
    public const uint TB_GETBUTTONTEXTW = 0x044B;
    public const byte TBSTATE_ENABLED = 0x04;
    public const byte TBSTATE_HIDDEN = 0x08;
    public const uint TVM_GETNEXTITEM = 0x110A;
    public const uint TVM_SELECTITEM = 0x110B;
    public const uint TVM_EXPAND = 0x1102;
    public const uint TVM_GETITEMRECT = 0x1104;
    public const uint TVM_GETITEMA = 0x110C;
    public const uint TVM_ENSUREVISIBLE = 0x1114;
    public const int TVGN_ROOT = 0x0000;
    public const int TVGN_NEXT = 0x0001;
    public const int TVGN_CHILD = 0x0004;
    public const int TVGN_CARET = 0x0009;
    public const int TVE_EXPAND = 0x0002;
    public const uint TVIF_TEXT = 0x0001;
    public const uint TVIF_STATE = 0x0008;
    public const uint TVIF_HANDLE = 0x0010;
    public const uint TVIF_CHILDREN = 0x0040;
    public const uint TVIS_SELECTED = 0x0002;
    public const int TVC_BYMOUSE = 0x0001;
    public const int TVN_SELCHANGEDA = -402;
    public const int TVN_SELCHANGEDW = -451;
    public const uint LVM_GETITEMCOUNT = 0x1004;
    public const uint LVM_GETITEMRECT = 0x100E;
    public const uint LVM_ENSUREVISIBLE = 0x1013;
    public const uint LVM_SETITEMSTATE = 0x102B;
    public const uint LVM_GETITEMTEXTA = 0x102D;
    public const uint LVM_GETITEMTEXTW = 0x1073;
    public const uint LVIF_TEXT = 0x0001;
    public const int LVIR_BOUNDS = 0;
    public const uint LVIS_FOCUSED = 0x0001;
    public const uint LVIS_SELECTED = 0x0002;
    public const uint ProcessVmOperation = 0x0008;
    public const uint ProcessVmRead = 0x0010;
    public const uint ProcessVmWrite = 0x0020;
    public const uint ProcessQueryInformation = 0x0400;
    public const uint MemCommit = 0x1000;
    public const uint MemReserve = 0x2000;
    public const uint MemRelease = 0x8000;
    public const uint PageReadWrite = 0x04;
    public const uint GA_ROOT = 2;
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    public const int GWLP_WNDPROC = -4;
    public const int GCLP_WNDPROC = -24;
    public const uint TH32CS_SNAPMODULE = 0x00000008;
    public const uint TH32CS_SNAPMODULE32 = 0x00000010;
    private const uint MF_BYPOSITION = 0x00000400;

    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextA", CharSet = CharSet.Ansi)]
    private static extern int GetWindowTextAnsi(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageText(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageA", CharSet = CharSet.Ansi)]
    private static extern IntPtr SendMessageTextAnsi(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool SetWindowText(IntPtr hWnd, string lpString);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true)]
    private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongW", SetLastError = true)]
    private static extern IntPtr GetClassLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    public static extern int GetDlgCtrlID(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    public static extern void MouseEvent(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr GetMenu(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetMenuItemCount(IntPtr hMenu);

    [DllImport("user32.dll")]
    public static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

    [DllImport("user32.dll")]
    public static extern uint GetMenuItemID(IntPtr hMenu, int nPos);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int cchMax, uint flags);

    [DllImport("user32.dll")]
    public static extern bool GetMenuItemRect(IntPtr hWnd, IntPtr hMenu, uint uItem, out NativeRect lprcItem);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    public static extern bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo guiThreadInfo);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool Module32First(IntPtr hSnapshot, ref ModuleEntry32 lpme);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool Module32Next(IntPtr hSnapshot, ref ModuleEntry32 lpme);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

    public static string GetText(IntPtr hwnd)
    {
        var sb = new StringBuilder(1024);
        GetWindowText(hwnd, sb, sb.Capacity);
        var text = sb.ToString();
        if (!string.IsNullOrEmpty(text)) return text;

        var wmText = new StringBuilder(1024);
        SendMessageText(hwnd, WM_GETTEXT, new IntPtr(wmText.Capacity), wmText);
        text = wmText.ToString();
        if (!string.IsNullOrEmpty(text)) return text;

        var ansi = new StringBuilder(1024);
        GetWindowTextAnsi(hwnd, ansi, ansi.Capacity);
        text = ansi.ToString();
        if (!string.IsNullOrEmpty(text)) return text;

        var wmAnsi = new StringBuilder(1024);
        SendMessageTextAnsi(hwnd, WM_GETTEXT, new IntPtr(wmAnsi.Capacity), wmAnsi);
        return wmAnsi.ToString();
    }

    public static string GetClass(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetMenuStringByPosition(IntPtr menu, int position)
    {
        var sb = new StringBuilder(512);
        GetMenuString(menu, (uint)position, sb, sb.Capacity, MF_BYPOSITION);
        return sb.ToString();
    }

    public static int TargetPointerSize(IntPtr hwnd)
    {
        if (!Environment.Is64BitOperatingSystem) return 4;
        GetWindowThreadProcessId(hwnd, out var pid);
        var process = OpenProcess(ProcessQueryInformation, false, pid);
        if (process == IntPtr.Zero) return IntPtr.Size;
        try
        {
            return IsWow64Process(process, out var isWow64) && isWow64 ? 4 : 8;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public static IntPtr GetWindowLongPtr(IntPtr hwnd, int index)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : GetWindowLong32(hwnd, index);

    public static IntPtr GetClassLongPtr(IntPtr hwnd, int index)
        => IntPtr.Size == 8 ? GetClassLongPtr64(hwnd, index) : GetClassLong32(hwnd, index);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MOVE = 0x0001;

    public static IntPtr MakeLParam(int x, int y)
        => new((y << 16) | (x & 0xFFFF));

    public static IntPtr MakeWParam(int low, int high)
        => new(((high & 0xFFFF) << 16) | (low & 0xFFFF));

    [StructLayout(LayoutKind.Sequential)]
    public struct GuiThreadInfo
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public NativeRect rcCaret;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct ModuleEntry32
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
