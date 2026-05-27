using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EasyRent_Checking.Models;

namespace EasyRent_Checking.Data
{
    public class EasyRent_CheckingContext : DbContext
    {
        public EasyRent_CheckingContext (DbContextOptions<EasyRent_CheckingContext> options)
            : base(options)
        {
        }

        public DbSet<EasyRent_Checking.Models.Driver> Driver { get; set; } = default!;
        public DbSet<EasyRent_Checking.Models.Vehicle> Vehicle { get; set; } = default!;
    }
}
