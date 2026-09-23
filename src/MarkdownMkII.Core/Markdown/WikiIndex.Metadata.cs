using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Markdown;

public static partial class WikiIndex
{
    /// <summary>Finds notes with front matter whose named field is missing or blank.</summary>
    public static IReadOnlyList<WikiBacklink> NotesWithoutField(
        IEnumerable<string> folders, string field, int maxHits = 80)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) ||
                !FrontMatter.TrySplit(text, out var fields, out _) ||
                (fields.TryGetValue(field, out var raw) && !string.IsNullOrWhiteSpace(raw)))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                break;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithoutDescription(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "description", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLicense(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "license", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCover(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "cover", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCanonical(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "canonical", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCategory(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "category", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutVersion(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "version", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSubtitle(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "subtitle", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPublisher(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "publisher", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAudience(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "audience", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCopyright(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "copyright", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSummary(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "summary", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLocation(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "location", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutRevision(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "revision", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSubject(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "subject", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutOrganization(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "organization", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutIdentifier(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "identifier", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDoi(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "doi", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAffiliation(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "affiliation", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutRights(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "rights", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutIssn(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "issn", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutIsbn(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "isbn", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAbstract(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "abstract", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPmid(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pmid", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutOrcid(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "orcid", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutConference(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "conference", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGrant(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "grant", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutFunder(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "funder", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutVolume(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "volume", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutIssue(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "issue", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPages(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pages", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutEditor(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "editor", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutChapter(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "chapter", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutEdition(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "edition", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutInstitution(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "institution", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSchool(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "school", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDepartment(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "department", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAdvisor(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "advisor", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDegree(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "degree", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutThesis(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "thesis", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutFaculty(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "faculty", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDiscipline(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "discipline", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLaboratory(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "laboratory", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCourse(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "course", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSemester(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "semester", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutModule(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "module", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCampus(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "campus", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutProgram(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "program", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBuilding(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "building", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutRoom(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "room", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutWing(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "wing", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutFloor(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "floor", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutOffice(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "office", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSuite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "suite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutHall(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "hall", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAnnex(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "annex", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTower(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "tower", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBlock(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "block", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPavilion(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pavilion", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutUnit(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "unit", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDesk(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "desk", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutZone(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "zone", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBay(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "bay", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAisle(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "aisle", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSector(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "sector", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCorridor(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "corridor", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLobby(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "lobby", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPlaza(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "plaza", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutFoyer(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "foyer", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCourtyard(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "courtyard", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAtrium(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "atrium", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTerrace(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "terrace", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGarden(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "garden", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPatio(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "patio", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPond(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pond", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDock(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "dock", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPier(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pier", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutHarbor(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "harbor", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutQuay(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "quay", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutMarina(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "marina", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutWharf(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "wharf", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBreakwater(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "breakwater", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutJetty(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "jetty", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLighthouse(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "lighthouse", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCove(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "cove", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutInlet(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "inlet", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutReef(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "reef", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutShoal(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "shoal", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDelta(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "delta", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutEstuary(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "estuary", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLagoon(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "lagoon", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSpit(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "spit", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAtoll(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "atoll", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutFjord(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "fjord", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCape(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "cape", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPeninsula(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "peninsula", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGulf(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "gulf", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutStrait(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "strait", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutIsthmus(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "isthmus", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutArchipelago(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "archipelago", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBayou(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "bayou", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTombolo(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "tombolo", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBight(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "bight", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutFirth(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "firth", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutRia(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "ria", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLoch(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "loch", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBasin(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "basin", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTrench(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "trench", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutShelf(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "shelf", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutRise(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "rise", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAbyss(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "abyss", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutRidge(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "ridge", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCanyon(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "canyon", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSlope(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "slope", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSeamount(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "seamount", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGuyot(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "guyot", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTrough(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "trough", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSill(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "sill", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutKnoll(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "knoll", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSpur(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "spur", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutMesa(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "mesa", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutButte(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "butte", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCliff(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "cliff", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBluff(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "bluff", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCrag(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "crag", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTor(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "tor", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPeak(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "peak", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSummit(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "summit", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCirque(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "cirque", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutArete(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "arete", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutScree(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "scree", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGully(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "gully", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutMoraine(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "moraine", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGlacier(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "glacier", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCouloir(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "couloir", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSerac(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "serac", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCrevasse(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "crevasse", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutHorn(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "horn", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutIcefall(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "icefall", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutNunatak(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "nunatak", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutFirn(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "firn", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutNeve(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "neve", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBergschrund(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "bergschrund", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutIcefield(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "icefield", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSaddle(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "saddle", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPass(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pass", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTarn(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "tarn", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutFell(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "fell", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCombe(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "combe", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDale(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "dale", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutKame(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "kame", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutEsker(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "esker", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDrumlin(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "drumlin", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutErratic(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "erratic", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLoess(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "loess", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTill(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "till", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutVarve(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "varve", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutOutwash(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "outwash", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutKettle(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "kettle", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPingo(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pingo", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPalsa(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "palsa", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLithalsa(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "lithalsa", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTalik(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "talik", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAlass(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "alass", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutNaled(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "naled", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAufeis(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "aufeis", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPolynya(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "polynya", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutFrazil(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "frazil", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSastrugi(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "sastrugi", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutZastruga(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "zastruga", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPenitente(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "penitente", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutRime(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "rime", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGraupel(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "graupel", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutHoar(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "hoar", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCornice(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "cornice", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSlab(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "slab", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBarchan(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "barchan", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSeif(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "seif", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutYardang(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "yardang", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutErg(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "erg", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDraa(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "draa", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutHamada(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "hamada", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutQanat(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "qanat", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutWadi(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "wadi", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSabkha(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "sabkha", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPlaya(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "playa", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCaliche(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "caliche", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSerir(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "serir", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutInselberg(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "inselberg", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBajada(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "bajada", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutOasis(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "oasis", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDune(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "dune", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPediment(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pediment", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutArroyo(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "arroyo", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTanezrouft(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "tanezrouft", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAlluvial(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "alluvial", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGibber(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "gibber", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutVentifact(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "ventifact", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutHoodoo(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "hoodoo", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTephra(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "tephra", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutLahar(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "lahar", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCaldera(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "caldera", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutMaar(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "maar", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutIgnimbrite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "ignimbrite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTumulus(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "tumulus", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutScoria(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "scoria", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPumice(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pumice", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPahoehoe(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pahoehoe", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPillow(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "pillow", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutObsidian(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "obsidian", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTuff(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "tuff", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutBasalt(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "basalt", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAndesite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "andesite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutRhyolite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "rhyolite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutDacite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "dacite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPhonolite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "phonolite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutXenolith(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "xenolith", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutTrachyte(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "trachyte", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutKimberlite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "kimberlite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutCarbonatite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "carbonatite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSchist(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "schist", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGneiss(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "gneiss", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutMarble(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "marble", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutQuartzite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "quartzite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSlate(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "slate", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutPhyllite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "phyllite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutAmphibolite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "amphibolite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutEclogite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "eclogite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutGranulite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "granulite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutMigmatite(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "migmatite", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutHornfels(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "hornfels", maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithoutSkarn(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithoutField(folders, "skarn", maxHits);
}
