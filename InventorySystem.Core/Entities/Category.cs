using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventorySystem.Core.Entities
{
    public class Category
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public int? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }
    }
}
