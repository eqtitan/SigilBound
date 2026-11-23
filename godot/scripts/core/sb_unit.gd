## A fighter in a battle (battle.js makeUnit()). A class rather than a Dictionary so identity
## checks (`r == u` in the web code) compare references.
class_name SbUnit
extends RefCounted

var key := ""
var side := "A"
var template: Dictionary
var name := ""
var el := "flame"
var max_hp := 0.0
var hp := 0.0
var atk := 0.0
var def := 0.0
var spd := 0.0
var crit := 15.0
var cdmg := 50.0
var tm := 0.0
var cds: Array = []
var effects: Array = [] # [{t, turns, fresh, val?}]
var alive := true
var dmg_dealt := 0.0
var level := 1
var stars := 1.0
var boss := false
var titan := false
var hero_uid := 0
var skill_lv: Array = []


func skills() -> Array:
	return template["skills"]
