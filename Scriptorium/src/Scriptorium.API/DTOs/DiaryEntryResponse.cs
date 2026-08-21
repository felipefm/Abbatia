using Scriptorium.Domain.Entities;

namespace Scriptorium.API.DTOs;

public sealed record DiaryEntryResponse(string Date, string Text, string UpdatedAtUtc)
{
    public static DiaryEntryResponse FromEntity(DiaryEntry entity) =>
        new(entity.Date.ToString("yyyy-MM-dd"), entity.Text, entity.UpdatedAtUtc.ToString("o"));
}

/// <summary>Corpo do PUT /api/diary/{date}.</summary>
public sealed record SaveDiaryEntryRequest(string Text);
