﻿using SGBackend.Entities;

namespace SGBackend.Models;

public class HideMedia
{
    public string mediumId { get; set; }
    
    public HiddenOrigin origin { get; set; }
}