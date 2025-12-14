using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services.Interfaces;

public interface IPlanService
{
    Plan BuildPlan(BackupMode mode, Snapshot source, Snapshot dest);
}
