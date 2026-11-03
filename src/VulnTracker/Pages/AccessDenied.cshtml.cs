using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VulnTracker.Pages;

// Logged-in users only (fallback policy), but no role needed.
public class AccessDeniedModel : PageModel;
