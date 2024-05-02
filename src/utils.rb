# File: utils.rb

require 'fileutils'

def print_banner(message)
  num_stars = message.length + 10
  print("\n#{'*' * num_stars}\n")
  print("#{' ' * 5}#{message}#{' ' * 5}\n")
  print("#{'*' * num_stars}\n")
end

def wrap_string(str, size)
  return str if str.length <= size

  num_chunks = str.length / size
  if (str.length % size > 0) then num_chunks += 1 end

  str_parts = Array.new(num_chunks)

  num_chunks.times do |i|
    chunk_start = size * i
    str_parts[i] = str[chunk_start, size]
  end

  return str_parts.join("\n")
end

def make_many_dirs(*paths)
  paths.each { |dir| FileUtils.mkdir_p(dir) }
end

def copy_to_many(src, *dests)
  dests.each { |target| FileUtils.cp_r(src, target) }
end
