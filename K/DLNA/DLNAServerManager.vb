Imports EasyK.DLNA.Player

Public Interface DLNAServerManager

    ''' <summary>
    ''' 重置歌词偏移
    ''' </summary>
    Sub ResetLyricsOffset()

    ''' <summary>
    ''' 设置DLNA播放器对象
    ''' </summary>
    ''' <param name="Player">DLNA播放器对象</param>
    Sub SetDLNAPlayer(Player As DLNAPlayer)

End Interface
