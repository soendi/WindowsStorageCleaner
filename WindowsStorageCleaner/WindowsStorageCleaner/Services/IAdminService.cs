namespace WindowsStorageCleaner.Services;

public interface IAdminService
{
    bool IsRunningAsAdmin();
    bool RestartAsAdmin();
}
