# File: utils.rb

def print_banner(message)
  num_stars = message.length + 10
  print("\n#{'*' * num_stars}\n")
  print("#{' ' * 5}#{message}#{' ' * 5}\n")
  print("#{'*' * num_stars}\n")
end
