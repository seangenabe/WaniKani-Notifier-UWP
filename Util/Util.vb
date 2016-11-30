Imports Windows.Data.Json
Imports Windows.Storage
Imports Windows.Storage.Streams

Public Module Util

    <Extension>
    Public Function ItemOrDefault(ps As IPropertySet, key As String) As Object
        Try
            Return ps.Item(key)
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    <Extension>
    Public Async Function ReadFileAsync(folder As StorageFolder, name As String) As Task(Of IBuffer)
        Dim _file As StorageFile
        Try
            _file = Await folder.GetFileAsync(name)
        Catch ex As FileNotFoundException
            Return Nothing
        End Try
        Using stream = Await _file.OpenReadAsync()
            Using reader As New DataReader(stream)
                Await reader.LoadAsync(CUInt(stream.Size))
                Return reader.ReadBuffer(CUInt(stream.Size))
            End Using
        End Using
    End Function

    <Extension>
    Public Async Function WriteFileAsync(folder As StorageFolder, name As String, data As IBuffer) As Task
        Dim _file As StorageFile = Await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting)
        Using stream = Await _file.OpenTransactedWriteAsync()
            Using writer As New DataWriter(DirectCast(stream, IOutputStream))
                writer.WriteBuffer(data)
                Await writer.StoreAsync()
                writer.DetachStream()
            End Using
            Await stream.CommitAsync()
        End Using
    End Function

    <Extension>
    Public Sub AddRange(Of T)(collection As ICollection(Of T), items As IEnumerable(Of T))
        For Each item In items
            collection.Add(item)
        Next
    End Sub

    <Extension>
    Public Function GetNamedStringOrNothing(obj As JsonObject, key As String) As String
        Try
            Dim value = obj.Item(key)
            If value.ValueType = JsonValueType.Null Then
                Return Nothing
            End If
            Return value.GetString()
        Catch ex As KeyNotFoundException
            Return Nothing
        End Try
    End Function

End Module
