using Microsoft.Win32;

namespace MeloongCore.Wpf;

public static class Dialogs {

    /// <summary>
    /// Shows a "Save" dialog and returns the path chosen by the user, or <c>null</c> if canceled.
    /// <para/> Extensions in <paramref name="filter"/> do not include the <c>.</c>.
    /// </summary>
    public static string? SaveFile(string title, string defaultFileName = "", string? defaultDirectory = null, IEnumerable<(string Extension, string Display)>? filter = null) {
        var filters = filter?.ToList();
        var dialog = new SaveFileDialog {
            AddExtension = true,
            Title = title,
            FileName = defaultFileName
        };
        if (!string.IsNullOrEmpty(defaultDirectory) && DirectoryUtils.Exists(defaultDirectory!)) dialog.InitialDirectory = PathUtils.ToShortPath(defaultDirectory!);
        if (filters is not null) {
            dialog.Filter = filters.Select(f => $"{f.Display}(*.{f.Extension})|*.{f.Extension}").Join("|");
        }

        Logger.Info($"保存弹窗：{dialog.Title}（FileName={dialog.FileName}, InitialDirectory={dialog.InitialDirectory}, Filter={dialog.Filter}）");
        if (dialog.ShowDialog() != true) return null;

        var result = dialog.FileName;
        Logger.Info($"保存弹窗返回：{result}");
        if (!result.Contains(Path.DirectorySeparatorChar)) return null;
        // AddExtension may not work, so the extension is added manually (#8214)
        if (filters != null && !string.IsNullOrEmpty(result) && !filters.Any(f => result.EndsWithF("." + f.Extension, true))) {
            result += "." + filters[dialog.FilterIndex - 1].Extension;
            Logger.Warn($"选择文件的返回无扩展名，将会手动添加，修改后为：{result}");
        }
        return result;
    }

    /// <summary>
    /// Shows a "Select file" dialog and returns the file paths chosen by the user, or an empty array if canceled.
    /// <para/> When using <paramref name="filter"/>, extensions do not include the <c>.</c>.
    /// </summary>
    public static List<string> SelectFile(string title, bool multiselect, string? defaultDirectory = null, OneOf<IEnumerable<(string[] Extensions, string Display)>, string>? filter = null) {
        var dialog = new OpenFileDialog {
            AddExtension = true,
            CheckFileExists = true,
            Multiselect = multiselect,
            Title = title,
            ValidateNames = true
        };
        if (!string.IsNullOrEmpty(defaultDirectory) && DirectoryUtils.Exists(defaultDirectory!)) dialog.InitialDirectory = PathUtils.ToShortPath(defaultDirectory!);
        if (filter is not null) {
            if (filter.Value.Is<string>()) {
                dialog.Filter = filter.Value.As<string>();
            } else {
                dialog.Filter = filter.Value.As<IEnumerable<(string[] Extensions, string Display)>>().Select(f => {
                    var exts = string.Join(";", f.Extensions.Select(e => $"*.{e}"));
                    return $"{f.Display}({exts})|{exts}";
                }).Join("|");
            }
        }

        Logger.Info($"选择文件弹窗：{dialog.Title}（Multiselect={dialog.Multiselect}, InitialDirectory={dialog.InitialDirectory}, Filter={dialog.Filter}）");
        if (dialog.ShowDialog() != true) return [];

        var results = dialog.FileNames.Where(f => f.Contains(Path.DirectorySeparatorChar)).ToList();
        Logger.Info($"选择文件弹窗返回：{results.Join(",")}");
        return results;
    }

    /// <summary>
    /// Shows a "Select folder" dialog and returns the folder paths chosen by the user (ending with a separator), or an empty array if canceled.
    /// </summary>
    public static List<string> SelectFolder(string title, bool multiselect, string? defaultDirectory = null) {
        var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog {
            ShowNewFolderButton = true,
            Description = title,
            Multiselect = multiselect,
            UseDescriptionForTitle = true
        };
        if (!string.IsNullOrEmpty(defaultDirectory) && DirectoryUtils.Exists(defaultDirectory!)) dialog.SelectedPath = defaultDirectory;

        Logger.Info($"选择文件夹弹窗：{dialog.Description}（Multiselect={dialog.Multiselect}, SelectedPath={dialog.SelectedPath}）");
        if (dialog.ShowDialog() != true) return [];

        var results = dialog.SelectedPaths.Where(f => f.Contains(Path.DirectorySeparatorChar)).Select(PathUtils.AddSlashSuffix).ToList();
        Logger.Info($"选择文件夹弹窗返回：{results.Join(",")}");
        return results;
    }

}
