namespace NSchema.Schema;

/// <summary>GRANT privileges ON TABLE ... TO role</summary>
public record TableGrant(string Role, TablePrivilege Privileges);
