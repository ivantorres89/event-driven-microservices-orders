using FluentMigrator.Runner.VersionTableInfo;

namespace OrderProcess.DatabaseMigrations;

/// <summary>
/// Version table for FluentMigrator.
///
/// Keeps an explicit DB-level record of schema version.
/// </summary>
public sealed class ContosoVersionTable : DefaultVersionTableMetaData
{
    public override string TableName => "SchemaVersion";
    public override string ColumnName => "Version";
    public override string AppliedOnColumnName => "AppliedOn";
    public override string DescriptionColumnName => "Description";
    public override string UniqueIndexName => "UX_SchemaVersion_Version";
    public override string SchemaName => "dbo";
}
