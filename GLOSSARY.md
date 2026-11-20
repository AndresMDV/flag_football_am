# Flag Football

Shared domain language for passing and reception.

## Language

**Pass timing percentage**:
The timing value selected when a pass is released, used for both throw execution quality and reception probability within the receiver's catching range.
_Avoid_: Pass completion rate, completion percentage

**Reception probability**:
The likelihood that a receiver catches a pass that reaches their catching range. It is distinct from the accuracy of the throw.
_Avoid_: Pass accuracy, pass execution quality

**Reception trial**:
The single decision about catching one pass, eligible only when the ball first reaches the receiver's catching range.
_Avoid_: Per-frame catch roll, repeated reception attempt

**Ball carrier**:
The player currently in possession of the ball.
_Avoid_: Controlled player, when referring only to possession

**Pass execution quality**:
How accurately a throw reaches its intended interception target.
_Avoid_: Reception probability, catch chance
