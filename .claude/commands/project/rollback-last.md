---
name: rollback-last
description: 回滚最近一次改动（提交或工作区），先检查再执行
---

目标：安全回滚"上一次更改"，默认不做破坏性操作。

执行流程：
1. 先展示当前状态：
   - `git status --short`
   - `git log --oneline -n 3`
2. 询问用户回滚模式：
   - `revert_commit`（推荐，适合已提交分支）：`git revert --no-edit HEAD`
   - `soft_reset`（仅撤销最近提交，保留代码改动）：`git reset --soft HEAD~1`
   - `hard_reset`（危险，会丢弃最近提交和改动）：`git reset --hard HEAD~1`
   - `discard_worktree`（仅丢弃未提交改动）：`git restore --worktree --staged .`
3. 对 `hard_reset` 和 `discard_worktree` 必须进行二次确认：
   - 明确提示"此操作不可恢复"
   - 未得到确认，不执行
4. 执行后再次展示：
   - `git status --short`
   - `git log --oneline -n 3`

输出要求：
- 用简短中文解释"做了什么、回滚到哪里"
- 若失败，贴出关键错误并给出下一步建议
