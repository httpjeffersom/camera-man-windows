namespace CameraMan.Windows.Models;

public enum CameraStatus
{
    Loading,
    Connected,
    NotFound,
    PermissionDenied,
    Disconnected,
    Error,
}

public sealed record CameraDevice(string Id, string Name);

public static class CameraDeviceCycle
{
    /// <summary>Returns the next device id, wrapping around. Null when there are no devices.</summary>
    public static string? Next(IReadOnlyList<CameraDevice> devices, string? currentId)
    {
        if (devices.Count == 0)
        {
            return null;
        }

        var index = -1;
        for (var i = 0; i < devices.Count; i++)
        {
            if (devices[i].Id == currentId)
            {
                index = i;
                break;
            }
        }

        return devices[(index + 1) % devices.Count].Id;
    }

    /// <summary>Chooses the saved device if it still exists, otherwise the first available one.</summary>
    public static string? Resolve(IReadOnlyList<CameraDevice> devices, string? preferredId)
    {
        if (devices.Count == 0)
        {
            return null;
        }

        return devices.Any(d => d.Id == preferredId) ? preferredId : devices[0].Id;
    }
}
