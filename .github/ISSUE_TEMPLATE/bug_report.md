name: Bug 报告
description: 报告 RoundedTB Revived 的问题(请务必附上日志)
title: "[Bug] "
labels: ["bug"]
body:
  - type: markdown
    attributes:
      value: |
        ## 感谢反馈!
        请尽量填全以下信息。其中**日志**对定位问题至关重要,请在复现问题后按下方提示提供 `%LOCALAPPDATA%\rtb.log` 的内容。
  - type: textarea
    id: description
    attributes:
      label: 问题描述
      description: 清晰描述你遇到的问题。
      placeholder: 例如:启用动态模式后,运行中的应用被裁剪……
    validations:
      required: true
  - type: textarea
    id: steps
    attributes:
      label: 复现步骤
      description: 一步步如何复现。
      placeholder: |
        1. 打开设置,启用……
        2. 打开某个应用……
        3. 观察到……
    validations:
      required: true
  - type: textarea
    id: expected
    attributes:
      label: 预期行为
      description: 你期望发生什么?
  - type: textarea
    id: actual
    attributes:
      label: 实际行为
      description: 实际发生了什么?
  - type: input
    id: os
    attributes:
      label: 系统版本
      description: 例如 Windows 11 24H2 (26100) / Windows 10 22H2
      placeholder: Windows 11 24H2 (26100)
    validations:
      required: true
  - type: input
    id: version
    attributes:
      label: RoundedTB Revived 版本
      description: 例如 R4 / R4.1 / 从源码自行构建
      placeholder: R4.1
    validations:
      required: true
  - type: checkboxes
    id: options
    attributes:
      label: 相关设置(如有)
      options:
        - label: 动态模式
        - label: AutoHide(自动隐藏任务栏)
        - label: TranslucentTB 兼容
        - label: 开机自启
  - type: textarea
    id: logs
    attributes:
      label: 日志(重要)
      description: |
        请按以下步骤收集日志:
        1. 启动 RoundedTB Revived
        2. 复现问题
        3. 退出程序
        4. 在文件资源管理器地址栏输入 `%LOCALAPPDATA%\rtb.log` 回车打开日志文件
        5. 把内容粘贴到下面(可只贴最后几十行;日志较大时可先删除旧日志再复现,使内容更干净)

        附上日志能显著加快问题定位,感谢配合!
      render: shell
  - type: textarea
    id: notes
    attributes:
      label: 补充说明
      description: 其他任何你觉得有帮助的信息。
