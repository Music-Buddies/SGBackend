﻿using SGBackend.Entities;

namespace SGBackend.Models;

public class RecommendedMedia
{
    public string songTitle { get; set; }

    public string[] allArtists { get; set; }

    public bool explicitFlag { get; set; }

    public long listenedSeconds { get; set; }

    public MediumImage[] albumImages { get; set; }

    public string linkToMedia { get; set; }

    public string albumName { get; set; }

    public string releaseDate { get; set; }
}