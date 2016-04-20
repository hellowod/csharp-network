using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace net_plugin.net
{
    /// <summary>
    /// Desc: CS客户端段网络接口
    /// Author: xiangjinbao
    /// </summary>
    public interface INetObject
    {
        /// <summary>
        /// 网络初始化
        /// </summary>
        void Init();
        /// <summary>
        /// 网络层心跳
        /// </summary>
        void Update();
        /// <summary>
        /// 发送消息包
        /// </summary>
        /// <param name="bytes"></param>
        void Send(byte[] bytes);
        /// <summary>
        /// 接受数据包
        /// </summary>
        /// <returns></returns>
        byte[] Recv();
        /// <summary>
        /// 重新链接
        /// </summary>
        void ReConnect();
        /// <summary>
        /// 断开链接
        /// </summary>
        void DisConnect();
    }
}
