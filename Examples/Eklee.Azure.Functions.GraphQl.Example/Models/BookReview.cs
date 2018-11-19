﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Eklee.Azure.Functions.GraphQl.Example.Models
{
	public class BookReview
	{
		[Key]
		[Description("Id of book review.")]
		public string Id { get; set; }

		[Description("Id of Reviewer")]
		[Edge(typeof(Reviewer))]
		public string ReviewerId { get; set; }

		[Description("Id of book")]
		[Edge(typeof(Reviewer))]
		public string BookId { get; set; }

		[Description("Commentary by reviewer")]
		public string Comments { get; set; }

		[Description("1-5 starts rating")]
		public int Stars { get; set; }
	}
}
