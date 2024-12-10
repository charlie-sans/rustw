# File_name: main.py
# Project: text
import os
import sys

# write a file with contents hello world

print(f"writing to {os.getcwd()}/code.py")

with open("code.py","w") as File:
   File.write("print(\"hello world\")")

print("wrote the file, now running it with 'python3 code.py'")

# after writing the file, we run it.
with open("code.py","r") as F:
   exec(F.read())
print("reading the file code.py")

# after writing it and running we read it.
with open("code.py", "r") as F:
  print(F.read())
