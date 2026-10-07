using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TaskApp.Pages;

public class IndexModel(TaskStore store) : PageModel
{
    public List<TaskItem> Tasks { get; private set; } = [];

    [BindProperty]
    [Required(ErrorMessage = "Enter a task name.")]
    [StringLength(200, ErrorMessage = "Use 200 characters or fewer.")]
    public string Title { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public long? EditId { get; set; }

    public void OnGet()
    {
        Tasks = store.List();
        if (EditId.HasValue)
        {
            var task = Tasks.Find(task => task.Id == EditId);
            if (task is null)
            {
                EditId = null;
            }
            else
            {
                Title = task.Title;
            }
        }
    }

    public IActionResult OnPostSave()
    {
        Title = Title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(Title) && ModelState.IsValid)
        {
            ModelState.AddModelError(nameof(Title), "Enter a task name.");
        }
        if (!ModelState.IsValid)
        {
            Tasks = store.List();
            return Page();
        }
        if (EditId.HasValue)
        {
            store.Update(EditId.Value, Title);
        }
        else
        {
            store.Create(Title);
        }
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(long id)
    {
        store.Delete(id);
        return RedirectToPage();
    }

    public IActionResult OnPostToggle(long id)
    {
        store.Toggle(id);
        return RedirectToPage();
    }
}