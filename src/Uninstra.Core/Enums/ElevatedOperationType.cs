namespace Uninstra.Core.Enums;

public enum ElevatedOperationType
{
    MoveToQuarantine,
    RestoreFromQuarantine,
    PermanentlyDeleteQuarantineItem,
    DeleteRegistryKey,
    DeleteRegistryValue,
    RestoreRegistryItem,
    StopService,
    DeleteService,
    DeleteScheduledTask,
    RemoveStartupEntry,
    CreateRestorePoint
}
