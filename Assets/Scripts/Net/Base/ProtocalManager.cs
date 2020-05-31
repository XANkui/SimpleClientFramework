using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProtocalManager 
{
    /// <summary>
    /// 获取密钥的
    /// </summary>
    public static void SecretKeyRequest() {

        MessageSecret messageSecret = new MessageSecret();
        NetManager.Instance.SendMessage(messageSecret);
        NetManager.Instance.AddProtocalListener(ProtocolEnum.MessageSecret,(resmsg)=> {
            NetManager.Instance.SetSecretkey(((MessageSecret)resmsg).Secret);
            Debug.Log("获取到的密钥："+NetManager.Instance.Secretkey);
        });
    }

    /// <summary>
    /// 测试分包粘包
    /// </summary>
    public static void SocketTest()
    {

        MessageTest messageTest = new MessageTest();
        messageTest.RequestContent= "dslkflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdfdslkflks" +
            "djklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdfdslkflksdjklfj lskdjflksjdklfjklsdjfklsjd" +
            "klflksdfsdfsdfsdfdslkflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdfdslkflksdjklfj ls" +
            "kdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdfdslkflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfs" +
            "dfsdfdslkflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdfdslkflksdjklfj lskdjflksjdklfjkds" +
            "dslkflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdflkflksdjklfj lskdjflksjdklfjklsdjfklsjd" +
            "klflksdfsdfsdfsdflsdjfklsjdklflksdfsdfsdfsdfdslkflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfs" +
            "dfdslkflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdfdslkflksdjklfj lskdjflksjdklfjklsdjfkl" +
            "sjdklflksdfsdfsdfsdfdslkflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdfdslkflksdjklfj lskdj" +
            "flksjdklfjklsdjfklsjdklflksdfsdfsdfsdfdslkflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdfdsl" +
            "kflksdjklfj lskdjflksjdklfjklsdjfklsjdklflksdfsdfsdfsdf";
        NetManager.Instance.SendMessage(messageTest);
        NetManager.Instance.AddProtocalListener(ProtocolEnum.MessageTest, (resmsg) => {

            MessageTest msgTest = (MessageTest)resmsg;

            Debug.Log("测试回调服务器的数据：" + msgTest.ResponseContent);
        });
    }

    /// <summary>
    /// 注册协议提交
    /// </summary>
    /// <param name="registerType"></param>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="code"></param>
    /// <param name="callback"></param>
    public static void Register(RegisterType registerType,string username, string password, string code
        ,Action<RegisterResult> callback) {
        MessageRegister messageRegister = new MessageRegister();

        messageRegister.RegisterType = registerType;
        messageRegister.Username = username;
        messageRegister.Password = password;
        messageRegister.Code = code;
        NetManager.Instance.SendMessage(messageRegister);
        NetManager.Instance.AddProtocalListener(ProtocolEnum.MessageRegister,(resmsg)=>{
            MessageRegister message = (MessageRegister)resmsg;
            callback(message.Result);

        });
    }

    /// <summary>
    /// 登录协议提交
    /// </summary>
    /// <param name="loginType"></param>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="callback"></param>
    public static void Login(LoginType loginType, string username, string password,
        Action<LoginResult, string> callback) {
        MessageLogin messageLogin = new MessageLogin();
        messageLogin.LoginType = loginType;
        messageLogin.Username = username;
        messageLogin.Password = password;
        NetManager.Instance.SendMessage(messageLogin);
        NetManager.Instance.AddProtocalListener(ProtocolEnum.MessageLogin,(resmsg)=> {

            MessageLogin message = (MessageLogin)resmsg;
            callback(message.Result,message.Token);

        });
        
    }
}
