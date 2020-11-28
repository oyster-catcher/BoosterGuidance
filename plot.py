#!/usr/bin/python

import argparse
from math import radians,sin,cos,sqrt
import sys
import pylab as P
import numpy as np
from matplotlib.patches import Circle

def plot_line(ax,data,fx,fy,color='black',label='',linestyle='-'):
  xx=[float(d[fx]) for d in data]
  yy=[float(d[fy]) for d in data]
  ax.plot(xx,yy,color=color,label=label,linestyle=linestyle)


def plot_times(ax, times, color):
  for t in times:
    ax.axvspan(t-0.1,t+0.1,facecolor=color,alpha=0.5)

def plot_markers(ax,data,fx,fy,times,color='black',marker='o',markersize=4,alpha=1):
  cx = []
  cy = []
  for t in times:
    done = False
    for i,d in enumerate(data):
      if data[i]['time'] >= t and (not done):
        cx.append(data[i][fx])
        cy.append(data[i][fy])
        done = True
  ax.plot(cx,cy,color=color,marker=marker,markersize=markersize,linestyle='',alpha=alpha)


def plot(labels,dmin,dmax,tmax,vmax,ymax,
         accelmax,
         filenames=[],savepng=None,
         marktime=None):

  # Set up figures
  # altitude against time, and throttle
  fig = P.figure(1,figsize=(15,10))

  colors=['red','blue','green','black','pink','grey','purple','salmon']

  P.subplot2grid((3,5),(0,0), colspan=2, rowspan=1)
  ax1 = P.gca()
  ax1.set_xlabel("time (secs)")
  ax1.set_ylabel("downrange (m)")
  ax1.set_xlim([0,tmax])
  ax1.set_ylim([0,dmax])
  ax1.grid()

  P.subplot2grid((3,5),(1,0), colspan=2, rowspan=1)
  ax2 = P.gca()
  ax2.set_xlabel("time (secs)")
  ax2.set_ylabel("velocity (m/s)")
  ax2.set_xlim([0,tmax])
  ax2.set_ylim([0,vmax])
  ax2.grid()

  P.subplot2grid((3,5),(2,0), colspan=2, rowspan=1)
  ax3 = P.gca()
  ax3.set_xlabel("time (secs)")
  ax3.set_ylabel("mass (tons)")
  ax3.set_xlim([0,tmax])
  ax3.set_ylim([0,100])
  ax3.grid()

  #P.subplot2grid((3,5),(1,1), colspan=1, rowspan=1)
  #ax4 = P.gca()
  #ax4.set_xlabel("time")
  #ax4.set_ylabel("vy")
  #ax4.set_xlim([0,tmax])
  #ax4.set_ylim([vymin,vymax])
  #ax4.grid()

  #P.subplot2grid((3,5),(2,0), colspan=1, rowspan=1)
  #ax5 = P.gca()
  #ax5.set_xlabel("time")
  #ax5.set_ylabel("z")
  #ax5.set_xlim([0,tmax])
  #ax5.set_ylim([zmin,zmax])
  #ax5.grid()

  #P.subplot2grid((3,5),(2,1), colspan=1, rowspan=1)
  #ax6 = P.gca()
  #ax6.set_xlabel("time")
  #ax6.set_ylabel("vz")
  #ax6.set_xlim([0,tmax])
  #ax6.set_ylim([vzmin,vzmax])
  #ax6.grid()

  # Throttle
  P.subplot2grid((3,5),(0,2), colspan=1, rowspan=1)
  ax7 = P.gca()
  ax7.set_xlabel("time")
  ax7.set_ylabel("mag(accel)")
  ax7.set_xlim([0,tmax])
  ax7.set_ylim([0,accelmax*1.1])
  ax7.grid()

  # target error
  P.subplot2grid((3,5),(0,3), colspan=1, rowspan=1)
  ax8 = P.gca()
  ax8.set_xlabel("time (secs)")
  ax8.set_ylabel("target error (m)")
  ax8.set_xlim([0,tmax])
  ax8.set_ylim([0,1000])
  ax8.grid()

  # Attitude error
  P.subplot2grid((3,5),(0,4), colspan=1, rowspan=1)
  ax9 = P.gca()
  ax9.set_xlabel("time (secs)")
  ax9.set_ylabel("attitude error (degrees)")
  ax9.set_xlim([0,tmax])
  ax9.set_ylim([0,90])
  ax9.grid()

  # XY
  P.subplot2grid((3,5),(1,2), colspan=3, rowspan=2)
  ax10 = P.gca()
  ax10.set_xlabel("downrange (m)")
  ax10.set_ylabel("altitude (m)")
  ax10.set_xlim([dmin,dmax])
  ax10.set_ylim([0,ymax])
  ax10.grid()


  for di,filename in enumerate(filenames):
    col = colors[di]
    data = read_data(filename,info)
    thrust_times = []
    check_times = []
    targets = []
    bodyposition = None
    bodyradius = None
    amin = 0
    if 'amin' in info:
      amin = float(info['amin'])
    amax = 0
    if 'amax' in info:
      amax = float(info['amax'])
    rf = None
    if 'rf' in info:
      t=Vector3Time()
      rf = t.fromStr(info['rf'])
      targets.append(rf)
    if 'body.position' in info:
      t=Vector3Time();
      bodyposition = t.fromStr(info['body.position'])
    if 'body.radius' in info:
      bodyradius = float(info['body.radius'])
    solutions = []
    sln_Tmax = 0
    sln_fuelmin = 1000
    sln_fuelmax = 0

    for d in data:
      T=np.array([d['vx'],0,d['vz']])
      d['downrange'] = np.linalg.norm(T)
    plot_line(ax1,data,'time','downrange',color=col)
    plot_markers(ax1,data,'time','downrange',check_times,color=col)
    plot_markers(ax1,data,'time','downrange',[marktime],color=colors[di],markersize=10,alpha=0.5)

    for d in data:
      T=np.array([d['vx'],d['vy'],d['vz']])
      d['velocity'] = np.linalg.norm(T)
    plot_line(ax2,data,'time','velocity',color=col)
    plot_markers(ax2,data,'time','velocity',check_times,color=col)
    plot_markers(ax2,data,'time','velocity',[marktime],color=colors[di],markersize=10,alpha=0.5)

    plot_line(ax3,data,'time','totalMass',color=col)
    plot_markers(ax3,data,'time','totalMass',check_times,color=col)
    plot_markers(ax3,data,'time','totalMass',[marktime],color=colors[di],markersize=10,alpha=0.5)

    #plot_line(ax4,data,'time','vy',color=col)
    #plot_markers(ax4,data,'time','vy',check_times,color=col)
    #plot_markers(ax4,data,'time','vy',[marktime],color=colors[di],markersize=10,alpha=0.5)

    #plot_line(ax5,data,'time','z',color=col)
    #plot_markers(ax5,data,'time','z',check_times,color=col)
    #plot_targets(ax5,[(t.t,t.z) for t in targets])
    #plot_markers(ax5,data,'time','z',[marktime],color=colors[di],markersize=10,alpha=0.5)

    #plot_line(ax6,data,'time','vz',color=col)
    #plot_markers(ax6,data,'time','vz',check_times,color=col)
    #plot_markers(ax6,data,'time','vz',[marktime],color=colors[di],markersize=10,alpha=0.5)

    # plot desired magnitude of acceleration
    tdata = []
    for d in data:
      T=np.array([d['ax'],d['ay'],d['az']])
      d['mag_accel'] = np.linalg.norm(T)
    plot_line(ax7,data,'time','mag_accel',color=col)
    plot_markers(ax7,data,'time','mag_accel',check_times,color=col)
    plot_markers(ax7,data,'time','mag_accel',[marktime],color=colors[di],markersize=10,alpha=0.5)
    if 'amin' in data[0]: # continuos amin values
      plot_line(ax7,data,'time','amin',color=col,linestyle='--')
    elif amin:
      ax7.plot([0,data[-1]['time']],[amin,amin],color=col,linestyle='--')
    if 'amax' in data[0]: # continuos amax values
      plot_line(ax7,data,'time','amax',color=col,linestyle='--')
    elif amax:
      ax7.plot([0,data[-1]['time']],[amax,amax],color=col,linestyle='--')
    plot_times(ax7, thrust_times, color=col)

    if ax8:
      try:
        plot_line(ax8,data,'time','target_error')
      except KeyError:
        pass

    plot_line(ax9,data,'time','att_err',color=col)
    plot_markers(ax9,data,'time','att_err',[marktime],color=colors[di],markersize=10,alpha=0.5)


    # plot side view of X,Y
    xx,yy=[],[]
    plot_line(ax10,data,'x','y',color=colors[di],label=filenames[di])
    if marktime:
      plot_markers(ax10,data,'x','y',[marktime],color=colors[di],markersize=10,alpha=0.5)
    # Show checkpoints
    plot_markers(ax10,data,'x','y',check_times,color=colors[di])

  # Draw body
  if bodyposition:
    body = Circle((bodyposition.x,bodyposition.y), bodyradius, color='brown')
    ax10.add_artist(body)
    #ax10.plot([bodyposition.x,bodyposition.y], markersize=bodyradius)

  ax10.legend()

  fig.tight_layout(pad=0.5)
  if savepng:
    P.savefig(savepng)
  else:
    P.show()

def extract_items(line, lists=[]):
  d = {}
  for kv in line.split(" "):
    if '=' in kv:
      k,v = kv.split('=',1)
      if k not in lists:
        d[k] = v
      if k in lists:
        try:
          d[k].append(v)
        except:
          d[k] = [v]
  return d

def tryfloat(x):
  try:
    return float(x)
  except ValueError:
    return x

def read_data(fname, d):
  """Reads column data file, values space or tab separated. First line in column names.
     Comments lines with hash can contain key=value pairs which will be returned in d"""
  fields=None
  dat=[]
  for line in file(fname):
    line=line.strip("\n\r")
    if line.startswith("#"):
      dd = extract_items(line[1:], lists=['target'])
      d.update(dd)
      continue
    if not fields:
      fields = line.split(None)
    else:
      try:
        data = [tryfloat(x) for x in line.split(" ")]
        if len(data)==len(fields):
          dat.append( dict(zip(fields,data)) )
      except:
        pass
  return dat

parser = argparse.ArgumentParser(description='Plot vessel data logs (or solutions) with X,Y,Z,VX,VY,VZ and Throttle in multiple plots')
parser.add_argument('filename', nargs='+',
                    help='Filename of TAB-separated data file, first line contains column names. Should contain time,x,y,z,vx,vy,vz,ax,ay,ax')
parser.add_argument('--dmin', type=float, help='Minimum downrange', default=None)
parser.add_argument('--dmax', type=float, help='Maximum downrange', default=None)
parser.add_argument('--vmax', type=float, help='Maximum velocity', default=None)
parser.add_argument('--ymax', type=float, help='Maximum altitude', default=None)
parser.add_argument('--marktime', type=float, help='Put a marker a this time position', default=None)
parser.add_argument('--accelmax', type=float, help='Maximum acceleration', default=None)
parser.add_argument('--tmax', type=float, help='Maximum time', default=None)
parser.add_argument('--square', action='store_true', help='Make XY plot square (roughly as depends on window size)', default=False)
parser.add_argument('--savepng', help='PNG filename to save plot to', default=None)

args = parser.parse_args()

datas=[]
info={}
for filename in args.filename:
  datas.append(read_data(filename,info))

alldata = []
for data in datas:
  alldata = alldata + data

# Find min and max
if not args.dmax:
  args.dmax = max([sqrt(d['x']*d['x'] + d['z']*d['z']) for d in alldata])
if not args.vmax:
  args.vmax = max([sqrt(d['vx']*d['vx'] + d['vy']*d['vy'] * d['vz']*d['vz']) for d in alldata])
if not args.ymax:
  args.ymax = max([d['y'] for d in alldata])

if not args.accelmax:
  args.accelmax = 0
  for d in alldata:
    T=np.array([d['ax'],d['ay'],d['az']])
    args.accelmax = max(np.linalg.norm(T),args.accelmax)

if not args.tmax:
  args.tmax = max([d['time'] for d in alldata])

size = max(args.dmax*1.5,args.ymax)
args.dmin = -0.2*size/1.5
args.dmax = 0.8*size/1.5
args.ymax = size

plot(args.filename,
     dmin=args.dmin, dmax=args.dmax,tmax=args.tmax,vmax=args.vmax,ymax=args.ymax,
     filenames=args.filename,accelmax=args.accelmax,savepng=args.savepng,
     marktime=args.marktime)
