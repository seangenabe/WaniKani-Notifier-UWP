Imports Windows.Data.Json
Imports Windows.Storage
Imports Util

Friend NotInheritable Class WaniKaniStats

    Private Const MAX_ITEMS = 5
    Private Const SETTING_PREFIX = "WaniKaniCriticalItems"
    Private Const LAST_ACCESSED_SETTING = SETTING_PREFIX + ".LastAccessed"
    Private Const CRITICAL_ITEMS_SETTING = SETTING_PREFIX + ".Items"
    Private Const RADICAL_PROGRESSION_SETTING = SETTING_PREFIX + ".RadicalProgression"
    Private Const KANJI_PROGRESSION_SETTING = SETTING_PREFIX + ".KanjiProgression"
    Private Const LEVEL_SETTING = SETTING_PREFIX + ".Level"

    Private Sub New()
    End Sub

    Friend Shared Async Function GetStats(key As String) As Task(Of WaniKaniStats)
        If String.IsNullOrEmpty(key) Then
            Throw New ArgumentException($"{NameOf(key)} cannot be empty", NameOf(key))
        End If

        Dim settings = ApplicationData.Current.LocalSettings

        Dim lastAccessed As Date = New Date(CLng(If(settings.Values.ItemOrDefault(LAST_ACCESSED_SETTING), 0)))
        Dim criticalItemsJson As JsonArray = Nothing
        Dim level As UInteger
        Dim radicalProgression As Single
        Dim kanjiProgression As Single

        Dim responseDate As Date = Date.Now

        Dim hitCache = Date.Now - lastAccessed < TimeSpan.FromDays(1)
        If hitCache Then
            ' Retrieve critical items from cache
            criticalItemsJson = JsonArray.Parse(CStr(If(settings.Values.ItemOrDefault(CRITICAL_ITEMS_SETTING), "[]")))
            radicalProgression = CSng(settings.Values.ItemOrDefault(RADICAL_PROGRESSION_SETTING))
            kanjiProgression = CSng(settings.Values.ItemOrDefault(KANJI_PROGRESSION_SETTING))
            level = CUInt(settings.Values.ItemOrDefault(LEVEL_SETTING))
        Else
            ' Get latest critical item stats
            Await Task.WhenAll(
                {
                    Async Function() As Task
                        Dim criticalItemsResult = Await WaniKaniApi.UserRequest(key, "critical-items")
                        criticalItemsJson = criticalItemsResult.Data.GetNamedArray("requested_information")
                        responseDate = If(criticalItemsResult.ResponseDate?.Date, Date.Now)
                    End Function(),
                    Async Function() As Task
                        ' Get and parse level progression.
                        Dim levelProgressionResult = Await WaniKaniApi.UserRequest(key, "level-progression")
                        Dim lpr_data = levelProgressionResult.Data
                        level = CUInt(lpr_data.GetNamedObject("user_information").GetNamedNumber("level"))
                        Dim lpr_data_ri = lpr_data.GetNamedObject("requested_information")
                        radicalProgression = CSng(lpr_data_ri.GetNamedNumber("radicals_progress") /
                            lpr_data_ri.GetNamedNumber("radicals_total"))
                        kanjiProgression = CSng(lpr_data_ri.GetNamedNumber("kanji_progress") /
                            lpr_data_ri.GetNamedNumber("kanji_total"))
                    End Function()
                }
            )
        End If
        Dim criticalItems As New List(Of Item)
        Dim newJsonItems As New JsonArray()
        newJsonItems.AddRange(criticalItemsJson.Take(MAX_ITEMS))
        criticalItemsJson = newJsonItems
        For Each _item In criticalItemsJson
            Dim item = _item.GetObject()
            Dim character = item.GetNamedStringOrNothing("character")
            Dim image = item.GetNamedStringOrNothing("image")
            Dim completion = CUInt(If(item.GetNamedStringOrNothing("percentage"), "0"))
            Dim type = WaniKaniStats.GetTypeFromString(item.GetNamedStringOrNothing("type"))
            criticalItems.Add(New Item() With {
                              .Character = character,
                              .Image = image,
                              .Completion = completion,
                              .Type = type})
        Next

        ' Save to cache
        If Not hitCache Then
            settings.Values.Item(LAST_ACCESSED_SETTING) = responseDate.Ticks
            settings.Values.Item(CRITICAL_ITEMS_SETTING) = criticalItemsJson.ToString()
            settings.Values.Item(RADICAL_PROGRESSION_SETTING) = radicalProgression
            settings.Values.Item(KANJI_PROGRESSION_SETTING) = kanjiProgression
            settings.Values.Item(LEVEL_SETTING) = level
        End If

        Dim ret As New WaniKaniStats()
        ret.CriticalItems = criticalItems
        ret.RadicalProgression = radicalProgression
        ret.KanjiProgression = kanjiProgression
        ret.Level = level
        Return ret
    End Function

    Private Shared Function GetTypeFromString(s As String) As ItemType
        Select Case s
            Case "radical"
                Return ItemType.Radical
            Case "kanji"
                Return ItemType.Kanji
            Case "vocabulary"
                Return ItemType.Vocabulary
        End Select
        Return ItemType.Unknown
    End Function

    Friend Property CriticalItems As IEnumerable(Of Item)
    Friend Property RadicalProgression As Single
    Friend Property KanjiProgression As Single
    Friend Property Level As UInteger

    Friend Class Item
        Friend Property Character As String
        Friend Property Image As String
        Friend Property Completion As UInteger
        Friend Property Type As ItemType
    End Class

    Friend Enum ItemType
        Unknown = 0
        Radical
        Kanji
        Vocabulary
    End Enum

End Class
