using Microsoft.EntityFrameworkCore;
using SftpApi.Models;
using System;
using System.Collections.Generic;

namespace SftpApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
        public DbSet<SftpAuthKey> SftpAuthKeys { get; set; }
    }
}
