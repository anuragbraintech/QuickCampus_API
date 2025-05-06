using System;
using System.Collections.Generic;

namespace QuickCampus_DAL.Context;

public partial class MstSection
{
    public int SectionId { get; set; }

    public string? Section { get; set; }

    public int? SortOrder { get; set; }

    public int? ClientId { get; set; }

    public virtual ICollection<TblQuestion> TblQuestions { get; set; } = new List<TblQuestion>();
}
