using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace VSynthApp
{
    public interface IProjectStorage
    {
        Task SaveAsync(string path, List<BlockViewModel> blocks);
        Task<List<BlockViewModel>> LoadAsync(string path);
    }

    public class JsonProjectStorage : IProjectStorage
    {
        public async Task SaveAsync(string path, List<BlockViewModel> blocks)
        {
            string json = JsonSerializer.Serialize(blocks);
            await File.WriteAllTextAsync(path, json);
        }

        public async Task<List<BlockViewModel>> LoadAsync(string path)
        {
            string json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<BlockViewModel>>(json) ?? new List<BlockViewModel>();
        }
    }

    public interface IPathProvider
    {
        string GetDefaultProjectPath();
        string GetDefaultExportPath();
    }

    public class DesktopPathProvider : IPathProvider
    {
        public string GetDefaultProjectPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "VSynthProject.json");
        }

        public string GetDefaultExportPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "VSynthTrack.mp3");
        }
    }
}
