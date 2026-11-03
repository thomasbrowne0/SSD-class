using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace VulnTracker.Data;

// Users who have logged in through Keycloak, so findings can be assigned to them (FIND-04).
// Keycloak stays the source of truth. Only id and username are copied (data minimisation, GDPR).
public class AppUser
{
    // Keycloak user id (the "sub" claim).
    [MaxLength(100)]
    public required string Id { get; set; }

    [MaxLength(100)]
    public required string Username { get; set; }

    // Called on every login, so username changes in Keycloak are picked up.
    public static async Task SaveLoginAsync(AppDbContext db, ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue("sub");
        var username = principal.FindFirstValue("preferred_username");
        if (id is null || username is null)
        {
            return;
        }

        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            db.Users.Add(new AppUser { Id = id, Username = username });
        }
        else
        {
            user.Username = username;
        }
        await db.SaveChangesAsync();
    }
}
