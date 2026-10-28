using System;
using System.Collections.Generic;

namespace CineBook.Models;

public partial class AppSetting
{
    public string Key { get; set; } = null!;

    public string? Value { get; set; }
}
