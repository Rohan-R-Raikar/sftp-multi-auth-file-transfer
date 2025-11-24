using Microsoft.AspNetCore.Http;
using SftpApi.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SftpApi.Models.ViewModels
{
    public class SftpFileTransferViewModel
    {
        [Required]
        public int SelectedUserId { get; set; }

        public List<SftpAuthKey> Users { get; set; } = new();

        [Required]
        public IFormFile? File { get; set; }
    }
}
