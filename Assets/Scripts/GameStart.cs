using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStart : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        NetManager.Instance.Connect("127.0.0.1",8021);
        StartCoroutine(NetManager.Instance.CheckNet());
    }

    // Update is called once per frame
    void Update()
    {
        NetManager.Instance.Update();


        if (Input.GetKeyDown(KeyCode.A))
        {
            ProtocalManager.SocketTest();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            ProtocalManager.Register(RegisterType.Phone,"15816551847","aaaa","5454",(res)=> {
                switch (res)
                {
                    case RegisterResult.Success:

                        Debug.Log("注册成功");
                        break;
                    case RegisterResult.Fail:
                        Debug.LogError("注册失败");
                        break;
                    case RegisterResult.AlreadyExit:
                        Debug.LogError("账户已存在");
                        break;
                    case RegisterResult.WrongCode:
                        Debug.LogError("验证码错误");
                        break;
                    case RegisterResult.Forbidden:
                        Debug.LogError("该账户不允许注册，请联系客服");
                        break;
                    default:
                        break;
                }
            });
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            ProtocalManager.Login(LoginType.Phone, "15816551847", "aaaa", (res, token) => {
                switch (res)
                {
                    case LoginResult.Success:
                        Debug.Log("登陆成功");
                        Debug.Log("Token:"+token);
                        break;
                    case LoginResult.Fail:
                        Debug.LogError("登录失败");
                        break;
                    case LoginResult.WrongPwd:
                        Debug.LogError("密码错误");
                        break;
                    case LoginResult.UserNotExit:
                        Debug.LogError("用户不存在");
                        break;
                    case LoginResult.TimeoutToken:
                        Debug.LogError("Token 超时");
                        break;
                    default:
                        break;
                }
            });
        }

    }

    private void OnApplicationQuit()
    {
        NetManager.Instance.Close();
    }
}
