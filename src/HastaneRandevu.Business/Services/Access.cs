using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Business.Services;

/// <summary>Role checks shared by the services; a refusal is audited and answered with a fixed message.</summary>
internal static class Access
{
    /// <exception cref="AccessDeniedException">The session does not have <paramref name="role"/>.</exception>
    public static void RequireRole(UserSession session, AccountRole role, IAuditLog audit)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Role != role)
        {
            audit.Record(AuditEvent.AccessDenied, session.Role);
            throw new AccessDeniedException();
        }
    }
}
