using System.Collections.Generic;

namespace InventorySystem.Core.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // --- NEW: Parent/Child Logic ---
        public int? ParentId { get; set; } // Null = Main Category
        public Category? Parent { get; set; }

        // A category can have many sub-categories
        public ICollection<Category> SubCategories { get; set; } = new List<Category>();

        // Useful for displaying "Plumbing > PVC" in lists later
        public string FullName => Parent != null ? $"{Parent.Name} > {Name}" : Name;
    }
}