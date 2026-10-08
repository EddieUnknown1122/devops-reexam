using System.ComponentModel.DataAnnotations;

namespace CrudApp.Models;

public class Item
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}

// Request body for POST and PUT
public record ItemInput(string? Name, string? Description);