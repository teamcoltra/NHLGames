﻿Imports System.IO
Imports NHLGames.My.Resources
Imports NHLGames.Utilities

Namespace Objects
    Public Class GameWatchArguments
        Const DefaultSegment = 12
        Public Property GameDate As Date = Now
        Public Property Quality As StreamQualityEnum = StreamQualityEnum.best
        Public Property Is60Fps As Boolean = True
        Public Property Cdn As CdnTypeEnum = CdnTypeEnum.Akc
        Public Property Stream As GameStream = Nothing
        Public Property GameTitle As String = String.Empty
        Public Property PlayerPath As String = String.Empty
        Public Property PlayerType As PlayerTypeEnum = PlayerTypeEnum.None
        Public Property StreamerPath As String = String.Empty
        Public Property UseCustomStreamerArgs As Boolean = True
        Public Property CustomStreamerArgs As String = " --hls-segment-threads=2 --hls-segment-attempts=10 --hls-segment-timeout=60"
        Public Property UseCustomPlayerArgs As Boolean = True
        Public Property CustomPlayerArgs As String = " --file-caching=10000 --network-caching=10000 --cache=50000"
        Public Property UseOutputArgs As Boolean = False
        Public Property PlayerOutputPath As String = String.Empty
        Public Property StreamLiveRewind As Integer = 5
        Public Property GameIsOnAir As Boolean = False
        Public Property StreamLiveReplayCode As LiveStatusCodeEnum = LiveStatusCodeEnum.Live
        Public Property StreamerType As StreamerTypeEnum = StreamerTypeEnum.None
        Public Property StreamLiveReplay As LiveReplayEnum = LiveReplayEnum.StreamStarts

        Public Shared ReadOnly StreamerDefaultArgs As Dictionary(Of String, String) = New Dictionary(Of String, String)() From {
            {"--hls-segment-threads", "4"}, {"--hls-segment-attempts", "10"}, {"--hls-segment-timeout", "60"}}

        Public Shared ReadOnly MpvDefaultArgs As Dictionary(Of String, String) = New Dictionary(Of String, String)() From {
            {"--cache", "50000"}}

        Public Shared ReadOnly VlcDefaultArgs As Dictionary(Of String, String) = New Dictionary(Of String, String)() From {
            {"--file-caching", "10000"}, {"--network-caching", "10000"}}

        Public Overrides Function ToString() As String
            Return OutputArgs(False)
        End Function

        Public Overloads Function ToString(safeOutput As Boolean)
            Return OutputArgs(safeOutput)
        End Function

        Private Function GetStreamQuality() As String
            Dim selectedQualities = ""
            Dim addQuality = Quality
            Dim maxQuality = CType([Enum].GetValues(GetType(StreamQualityEnum)), Integer()).Max()
            While addQuality < maxQuality
                selectedQualities &= addQuality.ToString().Substring(1) & ","
                addQuality = addQuality + 1
            End While
            selectedQualities &= StreamQualityEnum.worst.ToString()
            Return selectedQualities
        End Function

        Private Function OutputArgs(safeOutput As Boolean) As String

            If String.IsNullOrEmpty(PlayerPath) OrElse PlayerType.Equals(PlayerTypeEnum.None) Then _
                Console.WriteLine(English.errorPlayerPathEmpty)

            Dim result = ""

            If UseOutputArgs Then
                result = OutputArgs()
            Else
                result = PlayerArgs() & ReplayArgs()
            End If

            result &= ProxyArgs()
            If UseCustomStreamerArgs Then result &= CustomStreamerArgs
            If Not safeOutput Then result &= NhlCookieArgs()
            If Not safeOutput Then result &= UserAgentArgs()
            result &= If(safeOutput, StreamLinkCensoredArgs(), StreamLinkArgs())
            result &= If(Is60Fps, StreamBestQualityArgs(), StreamQualityArgs())

            Return result
        End Function

        Private Function ThreadArgs() As String
            Return If(UseOutputArgs,
                "--hls-segment-threads=4 --hls-segment-attempts=10 --hls-segment-timeout=60 ",
                "--hls-segment-threads=2 --hls-segment-attempts=5 --hls-segment-timeout=30 ")
        End Function

        Private Function PlayerArgs() As String
            Dim literalPlayerArgs = If(UseCustomPlayerArgs, $" {CustomPlayerArgs} ", String.Empty)
            Select Case PlayerType
                Case PlayerTypeEnum.Mpv
                    Return $"--player ""{PlayerPath} --force-window=immediate --title """"{GameTitle _
                            }"""" --user-agent=User-Agent=""""{Common.UserAgent}""""{literalPlayerArgs}"" "
                Case PlayerTypeEnum.Vlc
                    Return $"--player ""{PlayerPath} --meta-title """"{GameTitle}""""{literalPlayerArgs}"" "
                Case PlayerTypeEnum.Mpc
                    Return $"--player ""{PlayerPath}{literalPlayerArgs}"" "
            End Select
            Return String.Empty
        End Function

        Private Function ReplayArgs() As String
            ' v1.3.2: Seeking through live game only works with mpv (presetHls)
            Dim presetHls = If(PlayerType.Equals(PlayerTypeEnum.Mpv),
                               "--player-passthrough=hls ",
                               String.Empty)
            If GameIsOnAir Then
                If Not StreamLiveReplayCode.Equals(LiveStatusCodeEnum.Live) Then
                    Return $"--hls-live-edge={ReplayMinutes()} "
                Else
                    Return presetHls
                End If
            Else
                Return presetHls
            End If
        End Function

        Private Function ReplayMinutes() As Integer
            Select Case StreamLiveReplayCode
                Case LiveStatusCodeEnum.Rewind
                    Return StreamLiveRewind * DefaultSegment
                Case LiveStatusCodeEnum.Replay
                    Dim segments = CType((Date.UtcNow - GameDate).TotalMinutes, Integer) * DefaultSegment
                    If segments <= 0 Then StreamLiveReplay = LiveReplayEnum.StreamStarts
                    Select Case StreamLiveReplay
                        Case LiveReplayEnum.PuckDrop
                            Return segments - (DefaultSegment * 10)
                        Case LiveReplayEnum.GameTime
                            Return segments
                    End Select
                    Return 9999
            End Select
            Return DefaultSegment
        End Function

        Private Function ProxyArgs() As String
            Return String.Format("--https-proxy=""127.0.0.1:{0}"" ", NHLGamesMetro.MLBAMProxy.port)
        End Function

        Private Function NhlCookieArgs() As String
            Return $" --http-cookie=""mediaAuth={Common.GetRandomString(240)} """
        End Function

        Private Function UserAgentArgs() As String
            Return $" --http-header=""User-Agent={Common.UserAgent}"" "
        End Function

        Private Function StreamLinkArgs() As String
            Return $"""hlsvariant://{Stream.StreamUrl.Replace("CDN", Cdn.ToString().ToLower())} "
        End Function

        Private Function StreamLinkCensoredArgs() As String
            Return $"""hlsvariant://{English.msgCensoredStream} "
        End Function

        Private Function StreamBestQualityArgs() As String
            Return $"name_key=bitrate"" best --http-no-ssl-verify "
        End Function

        Private Function StreamQualityArgs() As String
            Return $""" {GetStreamQuality()} --http-no-ssl-verify "
        End Function

        Private Function OutputArgs() As String
            Dim outputPath As String = PlayerOutputPath.
                    Replace("(DATE)", DateHelper.GetPacificTime(Stream.Game.GameDate).ToString("yyyy-MM-dd")).
                    Replace("(HOME)", Stream.Game.HomeAbbrev).
                    Replace("(AWAY)", Stream.Game.AwayAbbrev).
                    Replace("(TYPE)", Stream.Type.ToString()).
                    Replace("(NETWORK)", Stream.Network)
            Dim suffix = 1
            Dim originalName = Path.GetFileNameWithoutExtension(outputPath)
            Dim originalExt = Path.GetExtension(outputPath)
            While (My.Computer.FileSystem.FileExists(outputPath))
                outputPath = Path.ChangeExtension(
                    Path.Combine(Path.GetDirectoryName(outputPath), originalName & "_" & suffix), originalExt)
                suffix += 1
            End While

            Return $"-f -o ""{outputPath}"" "
        End Function
    End Class
End Namespace
